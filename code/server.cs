using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;

public static class server
{
    /// <summary> A client connected to the server. </summary>
    class client
    {
        // The username + password of this client
        public string username { get; private set; }
        public byte[] password { get; private set; }

        /// <summary> The representation of this clients player object. </summary>
        public representation player
        {
            get => _player;
            set
            {
                if (_player != null)
                    throw new System.Exception("Client already has a player!");
                _player = value;
                player_representations[username] = value;
            }
        }
        representation _player;

        // The TCP connection to this client
        public TcpClient tcp { get; private set; }
        public NetworkStream stream { get; private set; }

        public float render_range = INIT_RENDER_RANGE;

        public client(TcpClient tcp)
        {
            this.tcp = tcp;
            stream = tcp.GetStream();
        }

        public void login(string username, byte[] password)
        {
            // Attempt to load the player
            representation player = null;
            if (!player_representations.TryGetValue(username, out player))
            {
                // Force the creation of the player on the client
                player = null;
                message_senders[MESSAGE.FORCE_CREATE](this,
                    Vector3.zero, player_prefab_local, player_prefab_remote,
                    ++representation.last_network_id_assigned, 0
                );
            }

            this.username = username;
            this.password = password;

            if (player != null)
            {
                this.player = player;
                player.transform.SetParent(active_representations);
                load(player, true, false);
            }
        }

        public void disconnect()
        {
            connected_clients.Remove(this);
            message_queues.Remove(this);
            stream.Close();
            tcp.Close();

            // Unload the player (also remove it from representations
            // so that it doens't just get re-loaded based on proximity)
            foreach (var c in connected_clients)
                if (c.has_loaded(player))
                    c.unload(player);

            player.transform.SetParent(inactive_representations);
        }

        /// <summary> The representations loaded as objects on this client. </summary>
        HashSet<representation> loaded = new HashSet<representation>();

        /// <summary> Returns true if the client should load the provided representation. </summary>
        bool should_load(representation rep)
        {
            return (rep.transform.position - player.transform.position).magnitude <
                rep.radius + render_range;
        }

        public void update_loaded()
        {
            if (player == null)
                return; // Only update loaded if we have a player

            // Loop over active representations
            foreach (Transform t in active_representations)
            {
                var rep = t.GetComponent<representation>();
                if (rep == null) continue;

                if (has_loaded(rep))
                {
                    // Unload from clients that are too far away
                    if (!should_load(rep))
                        unload(rep);
                }
                else
                {
                    // Load on clients that are within range
                    if (should_load(rep))
                        load(rep, false);
                }
            }
        }

        /// <summary> Returns true if the given representation is loaded on this client. </summary>
        public bool has_loaded(representation rep)
        {
            return loaded.Contains(rep);
        }

        /// <summary> Load an object corresponding to the given representation 
        /// on this client. </summary>
        public void load(representation rep, bool local, bool already_created = false)
        {
            // Load rep and all it's children
            network_utils.top_down<representation>(rep.transform, (loading) =>
            {
                if (already_created && loading != rep)
                    throw new System.Exception("A representation with children should not be already_created!");

                if (!already_created)
                {
                    MESSAGE m = local ? MESSAGE.CREATE_LOCAL : MESSAGE.CREATE_REMOTE;
                    message_senders[m](this, loading.serialize());
                }

                // Add this object to the loaded set
                loaded.Add(loading);
            });
        }

        /// <summary>  Unload the object corresponding to the given 
        /// representation on this client. </summary>
        public void unload(representation rep, bool already_removed = false)
        {
            // Unload rep and all of it's children
            network_utils.top_down<representation>(rep.transform, (unloading) =>
            {
                if (!loaded.Contains(unloading))
                {
                    string err = "Client " + username + " tried to unload an object (" + unloading.name +
                                 " id = " + unloading.network_id + ") which was not loaded!";
                    throw new System.Exception(err);
                }

                loaded.Remove(unloading);
            });

            // Let the client know that rep has been unloaded
            // (the client will automatically unload it's children also)
            if (!already_removed)
                message_senders[MESSAGE.UNLOAD](this, rep.network_id);
        }
    }


    //################//
    // REPRESENTATION //
    //################//


    /// <summary> Represents a networked object on the server. </summary>
    class representation : MonoBehaviour
    {
        /// <summary> The serialized values of networked_variables 
        /// beloning to this object. </summary>
        List<byte[]> serializations = new List<byte[]>();

        void set_serialization(int i, byte[] serial)
        {
            // Deal with special networked_variables
            if (i == (int)engine_networked_variable.TYPE.POSITION_X)
            {
                Vector3 local_pos = transform.localPosition;
                local_pos.x = System.BitConverter.ToSingle(serial, 0);
                transform.localPosition = local_pos;
            }
            else if (i == (int)engine_networked_variable.TYPE.POSITION_Y)
            {
                Vector3 local_pos = transform.localPosition;
                local_pos.y = System.BitConverter.ToSingle(serial, 0);
                transform.localPosition = local_pos;
            }
            else if (i == (int)engine_networked_variable.TYPE.POSITION_Z)
            {
                Vector3 local_pos = transform.localPosition;
                local_pos.z = System.BitConverter.ToSingle(serial, 0);
                transform.localPosition = local_pos;
            }

            if (serializations.Count > i) serializations[i] = serial;
            else if (serializations.Count == i) serializations.Add(serial);
            else throw new System.Exception("Tried to skip a serial!");
        }

        /// <summary> Called when the serialization 
        /// of a networked_variable changes. </summary>
        public void on_network_variable_change(
            client sender, int index, byte[] new_serialization)
        {
            // Store the serialization
            set_serialization(index, new_serialization);

            foreach (var c in connected_clients)
                if ((c != sender) && c.has_loaded(this))
                    message_senders[MESSAGE.VARIABLE_UPDATE](c, network_id, index, new_serialization);
        }

        /// <summary> My network id. Automatically updates the 
        /// representations[network_id] dictionary. </summary>
        public int network_id
        {
            get => _network_id;
            private set
            {
                representations.Remove(_network_id);
                if (representations.ContainsKey(value))
                    throw new System.Exception("Tried to overwrite representation id!");
                representations[value] = this;
                _network_id = value;
            }
        }
        int _network_id;

        // The prefab to create on new local clients
        public string local_prefab
        {
            get => _local_prefab;
            private set
            {
                _local_prefab = value;
                radius = networked.look_up(value).network_radius();
            }
        }
        string _local_prefab;

        // The prefab to create on new remote clients
        public string remote_prefab { get; private set; }

        // Needed for proximity tests
        public float radius { get; private set; }

        /// <summary> Serialize this representation into a form that can 
        /// be sent over the network, or saved to disk. </summary>
        public byte[] serialize()
        {
            // Parent_id = 0 if I am not a child of another networked_v2
            representation parent = transform.parent.GetComponent<representation>();
            int parent_id = parent == null ? 0 : parent.network_id;

            if (parent_id < 0)
                throw new System.Exception("Tried to set unregistered parent!");

            // Serialize the basic info needed to reproduce the object
            List<byte[]> to_send = new List<byte[]>
            {
                network_utils.encode_int(network_id),
                network_utils.encode_int(parent_id),
                network_utils.encode_string(local_prefab),
                network_utils.encode_string(remote_prefab)
            };

            // Serialize all saved network variables
            for (int i = 0; i < serializations.Count; ++i)
            {
                var serial = serializations[i];
                to_send.Add(network_utils.encode_int(serial.Length));
                to_send.Add(serial);
            }

            return network_utils.concat_buffers(to_send.ToArray());
        }

        /// <summary>  Create a network representation. This does not load the
        /// representation on any clients, or send creation messages. </summary>
        public static representation create(byte[] buffer, int offset, int length, out int input_id)
        {
            // Remember where the the end of the serialization is
            int end = offset + length;

            // Deserialize the basic info needed to reproduce the object
            input_id = network_utils.decode_int(buffer, ref offset);
            int parent_id = network_utils.decode_int(buffer, ref offset);
            string local_prefab = network_utils.decode_string(buffer, ref offset);
            string remote_prefab = network_utils.decode_string(buffer, ref offset);

            // Create the representation
            representation rep = new GameObject(local_prefab).AddComponent<representation>();
            if (parent_id > 0) rep.transform.SetParent(representations[parent_id].transform);
            else rep.transform.SetParent(active_representations);

            rep.local_prefab = local_prefab;
            rep.remote_prefab = remote_prefab;
            if (input_id < 0)
            {
                // This was a local id, assign a unique network id
                rep.network_id = ++last_network_id_assigned; // Network id's start at 1
            }
            else
            {
                // Restore the given network id
                rep.network_id = input_id;
                if (input_id > last_network_id_assigned)
                    last_network_id_assigned = input_id;
            }

            // Everything else is networked variables to deserialize
            int index = 0;
            while (offset < end)
            {
                byte[] serial = new byte[network_utils.decode_int(buffer, ref offset)];
                System.Buffer.BlockCopy(buffer, offset, serial, 0, serial.Length);
                offset += serial.Length;
                rep.set_serialization(index, serial);
                ++index;
            }

            return rep;
        }

        public static int last_network_id_assigned = 0;

#       if UNITY_EDITOR

        // The custom editor for server representations
        [UnityEditor.CustomEditor(typeof(representation), true)]
        class editor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                var rep = (representation)target;
                UnityEditor.EditorGUILayout.IntField("Network ID", rep.network_id);
            }
        }

#       endif

    }


    //##############//
    // SERVER LOGIC //
    //##############//

    // STATE VARIABLES //

    /// <summary> The render range for clients starts at this value. </summary>
    public const float INIT_RENDER_RANGE = 0f;

    /// <summary> The default port to listen on. </summary>
    public const int DEFAULT_PORT = 6969;

    /// <summary> The TCP listener the server is listening with. </summary>
    static TcpListener tcp;

    /// <summary> Returns true if the server has been started. </summary>
    public static bool started { get => tcp != null; }

    /// <summary> The name that this session is saved under. </summary>
    static string savename;

    /// <summary> The directory in which games are saved. </summary>
    public static string saves_dir()
    {
        // Ensure the saves/ directory exists
        string saves_dir = Application.persistentDataPath + "/saves";
        if (!System.IO.Directory.Exists(saves_dir))
            System.IO.Directory.CreateDirectory(saves_dir);
        return saves_dir;
    }

    /// <summary> The directory that this session is saved in. </summary>
    static string save_dir()
    {
        return saves_dir() + "/" + savename;
    }

    // Information about how to create new players
    static string player_prefab_local;
    static string player_prefab_remote;

    /// <summary> The clients currently connected to the server </summary>
    static HashSet<client> connected_clients = new HashSet<client>();

    /// <summary> Representations on the server, keyed by network id. </summary>
    static Dictionary<int, representation> representations = new Dictionary<int, representation>();

    /// <summary> Player representations on the server, keyed by username. </summary>
    static Dictionary<string, representation> player_representations = new Dictionary<string, representation>();

    /// <summary> The transform representing the server. </summary>
    static Transform transform
    {
        get
        {
            if (_transform == null)
                _transform = new GameObject("server").transform;
            return _transform;
        }
    }
    static Transform _transform;

    /// <summary> Transform containing active representations (those which are
    /// considered for existance on clients) </summary>
    static Transform active_representations
    {
        get
        {
            if (_active_representations == null)
            {
                _active_representations = new GameObject("active").transform;
                _active_representations.SetParent(transform);
            }
            return _active_representations;
        }
    }
    static Transform _active_representations;

    /// <summary> Representations that are not considered for existance
    /// on clients, but need to be remembered
    /// (such as logged out players) </summary>
    static Transform inactive_representations
    {
        get
        {
            if (_inactive_representations == null)
            {
                _inactive_representations = new GameObject("inactive").transform;
                _inactive_representations.SetParent(transform);
            }
            return _inactive_representations;
        }
    }
    static Transform _inactive_representations;

    /// <summary> A server message waiting to be sent. </summary>
    struct pending_message
    {
        public byte[] bytes;
        public float send_time;
    }

    /// <summary> Messages that are yet to be sent. </summary>
    static Dictionary<client, Queue<pending_message>> message_queues =
        new Dictionary<client, Queue<pending_message>>();

    // Traffic monitors
    static network_utils.traffic_monitor traffic_down;
    static network_utils.traffic_monitor traffic_up;

    // END STATE VARIABLES //


    /// <summary> Start a server listening on the given port on the local machine. </summary>
    public static void start(
        int port, string savename,
        string player_prefab_local, string player_prefab_remote)
    {
        server.player_prefab_local = player_prefab_local;
        server.player_prefab_remote = player_prefab_remote;

        if (!networked.look_up(player_prefab_local).GetType().IsSubclassOf(typeof(networked_player)))
            throw new System.Exception("Local player object must be a networked_player!");

        tcp = new TcpListener(network_utils.local_ip_address(), port);
        tcp.Start();

        server.savename = savename;
        if (System.IO.Directory.Exists(save_dir()))
            load();

        traffic_up = new network_utils.traffic_monitor();
        traffic_down = new network_utils.traffic_monitor();

        // Setup the message senders
        message_parsers = new Dictionary<global::client.MESSAGE, message_parser>
        {
            [global::client.MESSAGE.LOGIN] = (client, bytes, offset, length) =>
            {
                int init_offset = offset;
                string uname = network_utils.decode_string(bytes, ref offset);

                byte[] pword = new byte[length - (offset - init_offset)];
                System.Buffer.BlockCopy(bytes, offset, pword, 0, pword.Length);

                // Check if this username is in use
                foreach (var c in connected_clients)
                    if (c.username == uname)
                        throw new System.NotImplementedException();

                // Login
                client.login(uname, pword);
            },

            [global::client.MESSAGE.DISCONNECT] = (client, bytes, offset, legnth) =>
            {
                client.disconnect();
            },

            [global::client.MESSAGE.VARIABLE_UPDATE] = (client, bytes, offset, length) =>
            {
                // Forward the updated variable serialization to the correct representation
                int start = offset;
                int id = network_utils.decode_int(bytes, ref offset);
                int index = network_utils.decode_int(bytes, ref offset);
                int serial_length = length - (offset - start);
                byte[] serialization = new byte[serial_length];
                System.Buffer.BlockCopy(bytes, offset, serialization, 0, serial_length);
                representations[id].on_network_variable_change(client, index, serialization);
            },

            [global::client.MESSAGE.RENDER_RANGE_UPDATE] = (client, bytes, offset, length) =>
            {
                client.render_range = network_utils.decode_float(bytes, ref offset);
            },

            [global::client.MESSAGE.CREATE] = (client, bytes, offset, length) =>
            {
                // Create the representation from the info sent from the client
                int input_id;
                var rep = representation.create(bytes, offset, length, out input_id);
                if (input_id > 0)
                {
                    // This was a forced create

                    if (rep.local_prefab == player_prefab_local)
                    {
                        // This was a forced player creation
                        client.player = rep;
                    }
                    else
                    {
                        throw new System.NotImplementedException();
                    }
                }

                client.load(rep, true, true);

                // If this is a child, load it on all other
                // clients that have the parent.
                var parent = rep.transform.parent.GetComponent<representation>();
                if (parent != null)
                    foreach (var c in connected_clients)
                        if (c != client)
                            if (c.has_loaded(parent))
                                c.load(rep, false);

                message_senders[MESSAGE.CREATION_SUCCESS](client, input_id, rep.network_id);
            },

            [global::client.MESSAGE.DELETE] = (client, bytes, offset, length) =>
            {
                // Find the representation being deleted
                int network_id = network_utils.decode_int(bytes, ref offset);
                var deleting = representations[network_id];

                // Unload the object with the above network_id 
                // from all clients + the server (children will
                // also be unloaded by the client).
                foreach (var c in connected_clients)
                    if (c.has_loaded(deleting))
                        c.unload(deleting, c == client);

                // Remove/destroy the representation + all children
                network_utils.top_down<representation>(deleting.transform,
                    (rep) => representations.Remove(rep.network_id));
                Object.Destroy(deleting.gameObject);
            }
        };

        // Send a payload to a client
        void send(client client, MESSAGE msg_type, byte[] payload)
        {
            byte[] to_send = network_utils.concat_buffers(
                network_utils.encode_int(payload.Length),
                new byte[] { (byte)msg_type },
                payload
            );

            // Queue the message, creating the queue for this
            // client if it doesn't already exist
            Queue<pending_message> queue;
            if (!message_queues.TryGetValue(client, out queue))
            {
                queue = new Queue<pending_message>();
                message_queues[client] = queue;
            }

            queue.Enqueue(new pending_message
            {
                bytes = to_send,
                send_time = Time.realtimeSinceStartup
            });
        }

        // Setup the message senders
        message_senders = new Dictionary<MESSAGE, message_sender>
        {
            [MESSAGE.CREATE_LOCAL] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                send(client, MESSAGE.CREATE_LOCAL, (byte[])args[0]);
            },

            [MESSAGE.CREATE_REMOTE] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                send(client, MESSAGE.CREATE_REMOTE, (byte[])args[0]);
            },

            [MESSAGE.FORCE_CREATE] = (client, args) =>
            {
                if (args.Length != 5)
                    throw new System.ArgumentException("Wrong number of arguments!");

                Vector3 position = (Vector3)args[0];
                string local_prefab = (string)args[1];
                string remote_prefab = (string)args[2];
                int network_id = (int)args[3];
                int parent_id = (int)args[4];

                send(client, MESSAGE.FORCE_CREATE, network_utils.concat_buffers(
                    network_utils.encode_vector3(position),
                    network_utils.encode_string(local_prefab),
                    network_utils.encode_string(remote_prefab),
                    network_utils.encode_int(network_id),
                    network_utils.encode_int(parent_id)
                ));
            },

            [MESSAGE.UNLOAD] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];

                send(client, MESSAGE.UNLOAD, network_utils.encode_int(network_id));
            },

            [MESSAGE.VARIABLE_UPDATE] = (client, args) =>
            {
                if (args.Length != 3)
                    throw new System.ArgumentException("Wrong number of arguments!");

                var id = (int)args[0];
                var index = (int)args[1];
                var serialization = (byte[])args[2];

                send(client, MESSAGE.VARIABLE_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_int(id),
                    network_utils.encode_int(index),
                    serialization
                ));
            },

            [MESSAGE.CREATION_SUCCESS] = (client, args) =>
            {
                if (args.Length != 2)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int local_id = (int)args[0];
                int network_id = (int)args[1];

                send(client, MESSAGE.CREATION_SUCCESS, network_utils.concat_buffers(
                    network_utils.encode_int(local_id),
                    network_utils.encode_int(network_id)
                ));
            }
        };
    }

    static void load()
    {
        // Find all the files to load, in alphabetical order
        List<string> files = new List<string>(System.IO.Directory.GetFiles(save_dir()));
        for (int i = 0; i < files.Count; ++i)
            files[i] = System.IO.Path.GetFileName(files[i]);

        // Ensure files are loaded in correct order
        files.Sort((f1, f2) =>
        {
            int i1 = int.Parse(f1.Split('_')[0]);
            int i2 = int.Parse(f2.Split('_')[0]);
            return i1.CompareTo(i2);
        });

        foreach (var f in files)
        {
            // Get the filename + bytes
            byte[] bytes = System.IO.File.ReadAllBytes(save_dir() + "/" + f);
            var tags = f.Split('_');

            int input_id;
            var rep = representation.create(bytes, 0, bytes.Length, out input_id);
            if (input_id != rep.network_id)
                throw new System.Exception("Network id loaded incoorectly!");

            if (tags[1] == "player")
            {
                // This representation was a player; start inactive +
                // record the username.
                rep.transform.SetParent(inactive_representations);
                player_representations[tags[2]] = rep;
            }
            else if (tags[1] == "inrep")
            {
                // This was a top-level inactive representation, move it there
                if (rep.transform.parent == active_representations)
                    rep.transform.SetParent(inactive_representations);
            }
            else if (tags[1] == "rep")
            {
                // Active representations need no more work
            }
            else throw new System.Exception("Could not load " + f);
        }
    }

    static void save()
    {
        // Ensure the directory is blank
        if (System.IO.Directory.Exists(save_dir()))
            System.IO.Directory.Delete(save_dir(), true);
        System.IO.Directory.CreateDirectory(save_dir());

        // Remember which network_id's have been saved
        HashSet<int> saved = new HashSet<int>();

        // Save the reprentations in top-down order
        int order = 0;

        // Save the players first
        foreach (var kv in player_representations)
        {
            string fname = save_dir() + "/" + (++order) + "_player_" + kv.Key;
            System.IO.File.WriteAllBytes(fname, kv.Value.serialize());
            saved.Add(kv.Value.network_id);
        }

        // Then save active representations
        network_utils.top_down<representation>(active_representations, (rep) =>
        {
            if (saved.Contains(rep.network_id)) return;
            string fname = save_dir() + "/" + (++order) + "_rep";
            System.IO.File.WriteAllBytes(fname, rep.serialize());
            saved.Add(rep.network_id);
        });

        // Then save inactive representations
        network_utils.top_down<representation>(inactive_representations, (rep) =>
        {
            if (saved.Contains(rep.network_id)) return;
            string fname = save_dir() + "/" + (++order) + "_inrep";
            System.IO.File.WriteAllBytes(fname, rep.serialize());
            saved.Add(rep.network_id);
        });
    }

    public static void stop()
    {
        if (tcp == null) return;
        tcp.Stop();
        save();
    }

    public static void update()
    {
        if (tcp == null) return;

        // Connect new clients
        while (tcp.Pending())
            connected_clients.Add(new client(tcp.AcceptTcpClient()));

        // Recive messages from clients
        foreach (var c in new List<client>(connected_clients))
        {
            byte[] buffer = new byte[c.tcp.ReceiveBufferSize];
            while (c.stream.CanRead && c.stream.DataAvailable)
            {
                int bytes_read = c.stream.Read(buffer, 0, buffer.Length);
                int offset = 0;
                traffic_down.log_bytes(bytes_read);

                while (offset < bytes_read)
                {
                    // Parse message length
                    int payload_length = network_utils.decode_int(buffer, ref offset);

                    // Parse message type
                    var msg_type = (global::client.MESSAGE)buffer[offset];
                    offset += 1;

                    // Handle the message
                    message_parsers[msg_type](c, buffer, offset, payload_length);
                    offset += payload_length;
                }
            }
        }

        // Update the objects which are loaded on the clients
        foreach (var c in connected_clients)
            c.update_loaded();

        // Send the messages from the queue
        var disconnected_during_write = new List<client>();
        foreach (var kv in message_queues)
        {
            var client = kv.Key;
            var queue = kv.Value;

            // The buffer to concatinate messages into
            byte[] send_buffer = new byte[client.tcp.SendBufferSize];
            int offset = 0;

            while (queue.Count > 0)
            {
                var msg = queue.Dequeue();

                if (msg.bytes.Length > send_buffer.Length)
                    throw new System.Exception("Message too large!");

                if (offset + msg.bytes.Length > send_buffer.Length)
                {
                    // Message would overrun buffer, send the buffer
                    // and create a new one
                    try
                    {
                        traffic_up.log_bytes(offset);
                        client.stream.Write(send_buffer, 0, offset);
                    }
                    catch
                    {
                        disconnected_during_write.Add(client);
                    }
                    send_buffer = new byte[client.tcp.SendBufferSize];
                    offset = 0;
                }

                // Copy the message into the send buffer
                System.Buffer.BlockCopy(msg.bytes, 0, send_buffer, offset, msg.bytes.Length);
                offset += msg.bytes.Length; // Move to next message
            }

            // Send the buffer
            if (offset > 0)
            {
                try
                {
                    traffic_up.log_bytes(offset);
                    client.stream.Write(send_buffer, 0, offset);
                }
                catch
                {
                    disconnected_during_write.Add(client);
                }
            }
        }

        // Properly disconnect clients that were found
        // to have disconnected during message writing
        foreach (var d in disconnected_during_write)
            d.disconnect();
    }

    public static void draw_gizmos()
    {
        foreach (var c in connected_clients)
        {
            if (c.player == null)
                continue;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(c.player.transform.position, c.player.radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(c.player.transform.position, c.render_range);
        }
    }

    public static string info()
    {
        if (tcp == null) return "Server not started.";
        return "Server listening on " + tcp.LocalEndpoint + "\n" +
               connected_clients.Count + " clients connected\n" +
               representations.Count + " representations\n" +
               "Traffic:\n" +
               "    " + traffic_up.usage() + " up\n" +
               "    " + traffic_down.usage() + " down";
    }

    public enum MESSAGE : byte
    {
        CREATE_LOCAL = 1,  // Create a local network object on a client
        CREATE_REMOTE,     // Create a remote network object on a client
        FORCE_CREATE,      // Force a client to create an object
        UNLOAD,            // Unload an object on a client
        CREATION_SUCCESS,  // Send when a creation requested by a client was successful
        VARIABLE_UPDATE,   // Send a networked_variable update to a client
    }

    delegate void message_parser(client c, byte[] bytes, int offset, int length);
    static Dictionary<global::client.MESSAGE, message_parser> message_parsers;

    delegate void message_sender(client c, params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;
}
