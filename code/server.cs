using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;

public static class server
{
    //###########//
    // CONSTANTS //
    //###########//

    /// <summary> Clients that have been silent for longer than this are disconnected </summary>
    public const float CLIENT_TIMEOUT = 6f;

    /// <summary> How often a client should send a heartbeat 
    /// (both to avoid timeout, and to measure ping). </summary>
    public const float CLIENT_HEARTBEAT_PERIOD = 1f;

    /// <summary> The render range for clients starts at this value. </summary>
    public const float INIT_RENDER_RANGE = 0f;

    /// <summary> The default port to listen on. </summary>
    public const int DEFAULT_PORT = 6969;


    //########//
    // CLIENT //
    //########//


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

        // The last time we reccived a message from this client
        public float last_message_time = 0;

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
                    Vector3.zero, player_prefab,
                    ++representation.last_network_id_assigned, 0
                );
            }

            this.username = username;
            this.password = password;

            if (player != null)
            {
                this.player = player;
                player.transform.SetParent(active_representations);
                load(player);
            }
        }

        /// <summary> Called when a client disconnects. If message is not 
        /// null, it is sent to the server as part of a DISCONNECT message, 
        /// otherwise no DISCONNECT message is sent to the server. </summary>
        public void disconnect(string message, float timeout = CLIENT_TIMEOUT)
        {
            Debug.Log("Client " + username + " disconnected, message: " + message);

            // Send the disconnect message
            if (message != null)
                message_senders[MESSAGE.DISCONNECT](this, message);

            connected_clients.Remove(this);
            message_queues.Remove(this);

            // Close with a timeout, so that any hanging messages
            // (in particular the DISCONNECT message) can be sent.
            stream.Close((int)(timeout * 1000));
            tcp.Close();

            // Unload the player (also remove it from representations
            // so that it doens't just get re-loaded based on proximity)
            foreach (var c in connected_clients)
                if (c.has_loaded(player))
                    c.unload(player, false);

            player?.transform.SetParent(inactive_representations);
        }

        /// <summary> The representations loaded as objects on this client. </summary>
        HashSet<representation> loaded = new HashSet<representation>();

        /// <summary> Returns true if the client should load the provided representation. </summary>
        bool should_load(representation rep)
        {
            return (rep.transform.position - player.transform.position).magnitude <
                rep.radius + render_range;
        }

        /// <summary> Called once per network update, just after messages 
        /// are recived and just before messages are sent. </summary>
        public void update()
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
                        unload(rep, false);
                }
                else
                {
                    // Load on clients that are within range
                    if (should_load(rep))
                        load(rep, false);
                }
            }

            // How long since the last message was recived from this client
            float time_since_last_message = Time.realtimeSinceStartup - last_message_time;

            // Check if we've timed out, if so disconnect, but with
            // a large timeout to send remaining messages, in the
            // off chance that the client will actually recive them.
#if UNITY_EDITOR
            // Don't time out clients if the server is the editor, so that
            // we don't time people out if the editor is paused.
#else
            if (time_since_last_message > CLIENT_TIMEOUT)
                disconnect("Timed out", timeout: 10);
#endif
        }

        /// <summary> Returns true if the given representation is loaded on this client. </summary>
        public bool has_loaded(representation rep)
        {
            return loaded.Contains(rep);
        }

        /// <summary> Load an object corresponding to the given representation 
        /// on this client. </summary>
        public void load(representation rep, bool already_created = false)
        {
            // Load rep and all it's children
            network_utils.top_down<representation>(rep.transform, (loading) =>
            {
                if (already_created && loading != rep)
                    throw new System.Exception("A representation with children should not be already_created!");

                if (!already_created)
                    message_senders[MESSAGE.CREATE](this, loading.serialize());

                // Add this object to the loaded set
                loaded.Add(loading);
                loading.on_load_on(this);
            });
        }

        /// <summary>  Unload the object corresponding to the given 
        /// representation on this client. </summary>
        public void unload(representation rep, bool deleting, bool already_removed = false)
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

                // Remove this object from the loaded set
                loaded.Remove(unloading);
                unloading.on_unload_on(this);
            });

            // Let the client know that rep has been unloaded
            // (the client will automatically unload it's children also)
            if (!already_removed)
                message_senders[MESSAGE.UNLOAD](this, rep.network_id, deleting);
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

        /// <summary> The client which has authority over 
        /// this networked object. </summary>
        public client authority
        {
            get
            {
                // Check to see if my authority is still connected
                if (!connected_clients.Contains(_authority))
                    _authority = null;

                return _authority;
            }
            set
            {
                // Old client looses authority
                if (_authority != null)
                    message_senders[MESSAGE.LOSE_AUTH](_authority, network_id);

                _authority = value;

                // New client gains authority
                if (_authority != null)
                    message_senders[MESSAGE.GAIN_AUTH](_authority, network_id);
            }
        }
        client _authority;

        /// <summary> Remove the representation from the server, and  any corresponding objects from 
        /// clients. </summary>
        /// <param name="issued_from">The client that issed the delete, null if it was the server.</param>
        /// <param name="response_requested">True if the client that issued the delete wanted a response.</param>
        /// <param name="check_clients">True if the clients should be checked for objects to delete. This
        /// should only be false if it is guaranteed that no clients have the object loaded.</param>
        public void delete(client issued_from = null, bool response_requested = false,
            bool check_clients = true)
        {
            // Unload from all clients + the server (children 
            // will automatically be unloaded by the client).
            if (check_clients)
                foreach (var c in connected_clients)
                    if (c.has_loaded(this))
                        c.unload(this, true, already_removed: c == issued_from);

            // Move to inactive whilst deleting.
            transform.SetParent(inactive_representations);

            // Remove/destroy the representation + all children
            network_utils.top_down<representation>(transform, (rep) =>
            {
                // Move the id to the recently deleted collection
                representations.Remove(rep.network_id);
                recently_deleted[rep.network_id] = Time.realtimeSinceStartup;
            });

            Destroy(gameObject);

            // Delete successful. If the client requested a response, send one.
            if (response_requested)
                message_senders[MESSAGE.DELETE_SUCCESS](issued_from, network_id);
        }

        public void on_load_on(client client)
        {
            // If I was loaded and don't have authority
            // then set the client that loaded me to the authority
            if (authority == null)
                authority = client;
        }

        public void on_unload_on(client client)
        {
            // If I was unloaded from my authority, find a 
            // new client that I am loaded on to take over. 
            // If there are no such clients set my authority 
            // to null.
            if (authority == client)
            {
                // We don't need to send a LOSE_AUTH message to
                // a client that has unloaded an object.
                _authority = null;

                foreach (var c in connected_clients)
                    if (c.has_loaded(this))
                    {
                        authority = c;
                        break;
                    }
            }

            if (_authority == null && !persistant)
            {
                // Not loaded on any clients, and should not persist => should be deleted.
                delete(check_clients: false);
            }
        }

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

        /// <summary> The prefab to create on new clients. </summary>
        public string prefab
        {
            get => _prefab;
            private set
            {
                _prefab = value;
                var nw = networked.look_up(value);
                radius = nw.network_radius();
                persistant = nw.persistant();
            }
        }
        string _prefab;

        /// <summary> Needed for proximity tests. </summary>
        public float radius { get; private set; }

        /// <summary> Should this representation persist when unloaded from all clients? </summary>
        public bool persistant { get; private set; }

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
                network_utils.encode_string(prefab)
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
            string prefab = network_utils.decode_string(buffer, ref offset);

            // Create the representation
            representation rep = new GameObject(prefab).AddComponent<representation>();
            if (parent_id > 0) rep.transform.SetParent(representations[parent_id].transform);
            else rep.transform.SetParent(active_representations);

            rep.prefab = prefab;
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

#if UNITY_EDITOR

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

#endif

    }


    //##############//
    // SERVER LOGIC //
    //##############//


    // STATE VARIABLES //

    /// <summary> The TCP listener the server is listening with. </summary>
    static TcpListener tcp;

    /// <summary> Returns true if the server has been started. </summary>
    public static bool started { get => tcp != null; }

    /// <summary> The name that this session is saved under. </summary>
    static string savename;

    // Information about how to create new players
    static string player_prefab;

    /// <summary> The clients currently connected to the server </summary>
    static HashSet<client> connected_clients;

    /// <summary> Representations on the server, keyed by network id. </summary>
    static Dictionary<int, representation> representations;

    /// <summary> Representations that were recently deleted on the server. </summary>
    static Dictionary<int, float> recently_deleted;

    /// <summary> Player representations on the server, keyed by username. </summary>
    static Dictionary<string, representation> player_representations;

    /// <summary> The transform representing the server. </summary>
    static Transform transform;

    /// <summary> Transform containing active representations (those which are
    /// considered for existance on clients) </summary>
    static Transform active_representations;

    /// <summary> Representations that are not considered for existance
    /// on clients, but need to be remembered
    /// (such as logged out players) </summary>
    static Transform inactive_representations;

    /// <summary> Messages that are yet to be sent. </summary>
    static Dictionary<client, Queue<pending_message>> message_queues;

    // Traffic monitors
    static network_utils.traffic_monitor traffic_down;
    static network_utils.traffic_monitor traffic_up;

    // END STATE VARIABLES //

    static representation try_get_rep(int id, bool error_on_fail = false, bool allow_recently_deleted = true)
    {
        if (!representations.TryGetValue(id, out representation rep))
        {
            // Don't flag a warning if this was recently deleted
            if (allow_recently_deleted && recently_deleted.ContainsKey(id))
                return null;

            // Couldn't find and wasn't recently deleted, throw an error/warning
            string msg = "Could not find the representation with id " + rep;
            if (error_on_fail) throw new System.Exception(msg);
            else Debug.LogWarning(msg);
            return null;
        }

        return rep;
    }


    /// <summary> A server message waiting to be sent. </summary>
    struct pending_message
    {
        public byte[] bytes;
        public float send_time;
    }

    /// <summary> Start a server listening on the given port on the local machine. </summary>
    public static void start(int port, string savename, string player_prefab)
    {
        if (started)
            throw new System.Exception("Server already running!");

        // Cleanup from previous run
        if (transform != null) Object.Destroy(transform.gameObject);
        if (active_representations != null) Object.Destroy(active_representations.gameObject);
        if (inactive_representations != null) Object.Destroy(inactive_representations.gameObject);

        // Initialize state variables
        server.player_prefab = player_prefab;
        server.savename = savename;
        tcp = new TcpListener(network_utils.local_ip_address(), port);
        traffic_up = new network_utils.traffic_monitor();
        traffic_down = new network_utils.traffic_monitor();
        connected_clients = new HashSet<client>();
        representations = new Dictionary<int, representation>();
        recently_deleted = new Dictionary<int, float>();
        player_representations = new Dictionary<string, representation>();
        message_queues = new Dictionary<client, Queue<pending_message>>();
        transform = new GameObject("server").transform;
        active_representations = new GameObject("active").transform;
        inactive_representations = new GameObject("inactive").transform;

        // Tidy up the heirarcy a bit
        active_representations.SetParent(transform);
        inactive_representations.SetParent(transform);

        // Check that server configuration is valid
        if (!networked.look_up(player_prefab).GetType().IsSubclassOf(typeof(networked_player)))
            throw new System.Exception("Local player object must be a networked_player!");

        // Start listening
        tcp.Start();

        // Load the world
        if (System.IO.File.Exists(save_file()))
            load();

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
                    {
                        client.disconnect("Username already in use.");
                        return;
                    }

                // Login
                client.login(uname, pword);
            },

            [global::client.MESSAGE.DISCONNECT] = (client, bytes, offset, legnth) =>
            {
                // No need to send a server.DISCONNECT message to
                // the client as they requested the disconnect
                client.disconnect(null);
            },

            [global::client.MESSAGE.HEARTBEAT] = (client, bytes, offset, length) =>
            {
                // This client is still kicking - respond so they can time the ping
                int heartbeat_key = network_utils.decode_int(bytes, ref offset);
                message_senders[MESSAGE.HEARTBEAT](client, heartbeat_key);
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
                try_get_rep(id)?.on_network_variable_change(client, index, serialization);
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

                    if (rep.prefab == player_prefab)
                    {
                        // This was a forced player creation
                        client.player = rep;
                    }
                    else
                    {
                        throw new System.NotImplementedException(
                            "Forced creation of non-players is not supported!");
                    }
                }

                // Let the client know that the creation was successful
                // (this is done before the load, so that the client that created
                //  it has the correct network id *before* it reccives 
                //  load/serialization messages)
                message_senders[MESSAGE.CREATION_SUCCESS](client, input_id, rep.network_id);

                // Register (load) the object on clients
                client.load(rep, true);

                // If this is a child, load it on all other
                // clients that have the parent.
                var parent = rep.transform.parent.GetComponent<representation>();
                if (parent != null)
                    foreach (var c in connected_clients)
                        if (c != client)
                            if (c.has_loaded(parent))
                                c.load(rep, false);
            },

            [global::client.MESSAGE.DELETE] = (client, bytes, offset, length) =>
            {
                int network_id = network_utils.decode_int(bytes, ref offset);
                bool response = network_utils.decode_bool(bytes, ref offset);

                // Find the representation being deleted
                representation deleting;
                if (!representations.TryGetValue(network_id, out deleting))
                {
                    // This should only happend in high-latency edge cases
                    Debug.Log("Deleting non-existant id " + network_id);
                    return;
                }

                // Delete the representation
                deleting.delete(issued_from: client, response_requested: response);
            }
        };

        // Send a payload to a client
        void send(client client, MESSAGE msg_type, byte[] payload, bool immediate = false)
        {
            byte[] to_send = network_utils.concat_buffers(
                network_utils.encode_int(payload.Length),
                new byte[] { (byte)msg_type },
                payload
            );

            if (immediate)
            {
                // Send the message immediately
                // this results in lower throughput and should only
                // be used when absolutely neccassary
                try
                {
                    client.stream.Write(to_send, 0, to_send.Length);
                }
                catch
                {
                    // Client was found to have disconnected
                    // during immediate message send (message
                    // = null because there would be no point
                    // trying to contact them, given they just
                    // disconnected).
                    client.disconnect(null);
                }
                return;
            }

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
            [MESSAGE.CREATE] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                send(client, MESSAGE.CREATE, (byte[])args[0]);
            },

            [MESSAGE.FORCE_CREATE] = (client, args) =>
            {
                if (args.Length != 4)
                    throw new System.ArgumentException("Wrong number of arguments!");

                Vector3 position = (Vector3)args[0];
                string prefab = (string)args[1];
                int network_id = (int)args[2];
                int parent_id = (int)args[3];

                send(client, MESSAGE.FORCE_CREATE, network_utils.concat_buffers(
                    network_utils.encode_vector3(position),
                    network_utils.encode_string(prefab),
                    network_utils.encode_int(network_id),
                    network_utils.encode_int(parent_id)
                ));
            },

            [MESSAGE.UNLOAD] = (client, args) =>
            {
                if (args.Length != 2)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                bool deleting = (bool)args[1];

                send(client, MESSAGE.UNLOAD, network_utils.concat_buffers(
                    network_utils.encode_int(network_id),
                    network_utils.encode_bool(deleting)
                ));
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
            },

            [MESSAGE.DELETE_SUCCESS] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                send(client, MESSAGE.DELETE_SUCCESS, network_utils.encode_int(network_id));
            },

            [MESSAGE.GAIN_AUTH] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                if (network_id <= 0)
                    throw new System.Exception("Can't gain authority over unregistered object!");

                send(client, MESSAGE.GAIN_AUTH, network_utils.encode_int(network_id));
            },

            [MESSAGE.LOSE_AUTH] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                if (network_id <= 0)
                    throw new System.Exception("Can't lose authority over unregistered object!");

                send(client, MESSAGE.GAIN_AUTH, network_utils.encode_int(network_id));
            },

            [MESSAGE.HEARTBEAT] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int heartbeat_key = (int)args[0];
                var dt = System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
                int seconds_since_epoch = (int)dt.TotalSeconds; // See you in 2038!

                send(client, MESSAGE.HEARTBEAT, network_utils.concat_buffers(
                    network_utils.encode_int(heartbeat_key),
                    network_utils.encode_int(seconds_since_epoch)
                ));
            },

            [MESSAGE.DISCONNECT] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                // The disconnection message
                string msg = (string)args[0];
                if (msg == null)
                    throw new System.Exception("Disconnect messages should not be sent without a payload!");

                // Disconnect messages are sent immediately, so that the client object (including 
                // it's message queues) can be immediately removed afterwards
                send(client, MESSAGE.DISCONNECT, network_utils.encode_string(msg), immediate: true);
            }
        };
    }

    static void load()
    {
        string fullpath = System.IO.Path.GetFullPath(save_file());
        Debug.Log("Loading: " + fullpath);

        using (var file = System.IO.File.OpenRead(fullpath))
        using (var decompress = new System.IO.Compression.GZipStream(file,
            System.IO.Compression.CompressionMode.Decompress))
        using (var buffer = new System.IO.MemoryStream())
        {
            decompress.CopyTo(buffer);
            buffer.Seek(0, System.IO.SeekOrigin.Begin);

            int length = 0;
            byte[] length_bytes = new byte[sizeof(int)];

            while (true)
            {
                // Deserialize the type of the representation
                int type_int = buffer.ReadByte();
                if (type_int < 0) break;
                SAVE_TYPE type = (SAVE_TYPE)type_int;

                // Desrielize the length of the representation
                buffer.Read(length_bytes, 0, sizeof(int));
                length = System.BitConverter.ToInt32(length_bytes, 0);

                // Deserialize the representation
                byte[] rep_bytes = new byte[length];
                buffer.Read(rep_bytes, 0, length);
                var rep = representation.create(rep_bytes, 0, length, out int input_id);

                // Check the network id recovered makes sense
                if (input_id < 0) throw new System.Exception("Loaded unregistered representation!");
                if (input_id != rep.network_id) throw new System.Exception("Network id loaded incorrectly!");

                switch (type)
                {
                    case SAVE_TYPE.PLAYER:

                        // For players, deserialize also the username
                        buffer.Read(length_bytes, 0, sizeof(int));
                        length = System.BitConverter.ToInt32(length_bytes, 0);
                        byte[] uname_bytes = new byte[length];
                        buffer.Read(uname_bytes, 0, length);
                        string username = System.Text.Encoding.ASCII.GetString(uname_bytes);

                        // Players start inactive
                        rep.transform.SetParent(inactive_representations);
                        player_representations[username] = rep;
                        break;

                    case SAVE_TYPE.ACTIVE:

                        // Nothing needs doing
                        break;

                    case SAVE_TYPE.INACTIVE:

                        // If this is a top-level representation, move to inactive
                        if (rep.transform == active_representations)
                            rep.transform.SetParent(inactive_representations);
                        break;

                    default:
                        throw new System.Exception("Unkown save type: " + type);
                }
            }
        }
    }

    /// <summary> The byte identifying which kind of 
    /// object comes next in the save file. </summary>
    enum SAVE_TYPE : byte
    {
        PLAYER = 1,
        ACTIVE,
        INACTIVE
    }

    /// <summary> Extension method to write a variable-size byte array to a filestream. </summary>
    public static void write_bytes_with_length(this System.IO.Stream s, byte[] bytes)
    {
        byte[] size_bytes = System.BitConverter.GetBytes(bytes.Length);
        s.Write(size_bytes, 0, size_bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    static void save()
    {
        // The file containing the savegame
        using (var file = System.IO.File.OpenWrite(save_file()))
        using (var compressor = new System.IO.Compression.GZipStream(file,
            System.IO.Compression.CompressionLevel.Optimal))
        {
            // Remember which network_id's have been saved
            HashSet<int> saved = new HashSet<int>();

            // Save the players first
            foreach (var kv in player_representations)
            {
                compressor.WriteByte((byte)SAVE_TYPE.PLAYER);
                compressor.write_bytes_with_length(kv.Value.serialize());

                // Write the username
                var uname_bytes = System.Text.Encoding.ASCII.GetBytes(kv.Key);
                compressor.write_bytes_with_length(uname_bytes);

                saved.Add(kv.Value.network_id);
            }

            // Then save active representations
            network_utils.top_down<representation>(active_representations, (rep) =>
            {
                if (saved.Contains(rep.network_id) || !rep.persistant) return;
                compressor.WriteByte((byte)SAVE_TYPE.ACTIVE);
                compressor.write_bytes_with_length(rep.serialize());
                saved.Add(rep.network_id);
            });

            // Then save inactive representations
            network_utils.top_down<representation>(inactive_representations, (rep) =>
            {
                if (saved.Contains(rep.network_id) || !rep.persistant) return;
                compressor.WriteByte((byte)SAVE_TYPE.INACTIVE);
                compressor.write_bytes_with_length(rep.serialize());
                saved.Add(rep.network_id);
            });
        }
    }

    public static void stop()
    {
        if (!started) return;

        foreach (var c in new List<client>(connected_clients))
            c.disconnect("Server stopped.");

        tcp.Stop();
        save();
        tcp = null;
    }

    public static void update()
    {
        if (!started) return;

        // Timout recently-deleted id's
        HashSet<int> to_remove = new HashSet<int>();
        foreach (var kv in recently_deleted)
            if (Time.realtimeSinceStartup - kv.Value > CLIENT_TIMEOUT)
                to_remove.Add(kv.Key);

        foreach (var i in to_remove)
            recently_deleted.Remove(i);

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
                    if (!message_parsers.TryGetValue(msg_type, out message_parser parser))
                        throw new System.Exception("Unkown message " + msg_type);

                    c.last_message_time = Time.realtimeSinceStartup;
                    parser(c, buffer, offset, payload_length);
                    offset += payload_length;
                }
            }
        }

        // Update the objects which are loaded on the clients
        foreach (var c in new List<client>(connected_clients))
            c.update();

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
            d.disconnect(null);
    }

    /// <summary> The directory in which games are saved. </summary>
    static string saves_dir()
    {
        // Ensure the saves/ directory exists
        string saves_dir = Application.persistentDataPath + "/saves";
        if (!System.IO.Directory.Exists(saves_dir))
            System.IO.Directory.CreateDirectory(saves_dir);
        return saves_dir;
    }

    /// <summary> The directory that this session is saved in. </summary>
    static string save_file()
    {
        return saves_dir() + "/" + savename + ".save";
    }

    /// <summary> Get an array of all the save files on this machine. </summary>
    public static string[] existing_saves()
    {
        return System.IO.Directory.GetFiles(saves_dir());
    }

    /// <summary> Returns true if the save with the given name already exists. </summary>
    public static bool save_exists(string savename)
    {
        return System.IO.File.Exists(saves_dir() + "/" + savename + ".save");
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
        if (!started) return "Server not started.";
        return "Server listening on " + tcp.LocalEndpoint + "\n" +
               "    Connected clients  : " + connected_clients.Count + "\n" +
               "    Representations    : " + representations.Count + "\n" +
               "    Recently deleted   : " + recently_deleted.Count + "\n" +
               "    Upload             : " + traffic_up.usage() + "\n" +
               "    Download           : " + traffic_down.usage();
    }

    public enum MESSAGE : byte
    {
        // Numbering starts at 1 so erroneous 0's are caught
        CREATE = 1,        // Create a networked object on a client
        FORCE_CREATE,      // Force a client to create an object
        UNLOAD,            // Unload an object on a client
        CREATION_SUCCESS,  // Send when a creation requested by a client was successful
        DELETE_SUCCESS,    // Send when a client deletes a networked object and requests a response
        VARIABLE_UPDATE,   // Send a networked_variable update to a client
        LOSE_AUTH,         // Sent to a client when they lose authority over an object
        GAIN_AUTH,         // Sent to a ciient when they gain authority over an object
        HEARTBEAT,         // Respond to a client heartbeat
        DISCONNECT,        // Sent to a client when they are disconnected
    }

    delegate void message_parser(client c, byte[] bytes, int offset, int length);
    static Dictionary<global::client.MESSAGE, message_parser> message_parsers;

    delegate void message_sender(client c, params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;
}
