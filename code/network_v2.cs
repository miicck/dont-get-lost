using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;

/*
 * 
 * TODO
 * - User-defined data (serialization)
 * - Serialize to disk
 *  
 */

public class networked_v2 : MonoBehaviour
{
    /// <summary> My radius as far as the server is concerned 
    /// for determining if I can be seen. </summary>
    public virtual float network_radius() { return 1f; }

    /// <summary> How far I move before sending updated positions to the server. </summary>
    public virtual float position_resolution() { return 0.1f; }

    /// <summary> Returns false if position updates should not be sent (should only
    /// be set false for objects that can NEVER be moved on a client). </summary>
    public virtual bool sends_position_updates() { return true; }

    /// <summary> How fast I lerp my position. </summary>
    public virtual float lerp_amount() { return 5f; }

    public Vector3 networked_position
    {
        get => transform.position;
        set
        {
            transform.position = value;
            target_local_position = transform.localPosition;
        }
    }

    Vector3 last_sent_local_position;

    public void on_position_up_to_date()
    {
        last_sent_local_position = transform.localPosition;
    }

    /// <summary> Recive a position update from the server. </summary>
    public void recive_position_update(Vector3 new_local_position)
    {
        target_local_position = new_local_position;
    }
    Vector3 target_local_position;

    /// <summary> Run networking updates (called every frame by client). </summary>
    public virtual void network_update()
    {
        if (transform.parent == null)
        {
            Vector3 delta = target_local_position - transform.localPosition;
            if (delta.magnitude > position_resolution())
                transform.localPosition = Vector3.Lerp(transform.localPosition, target_local_position, Time.deltaTime * lerp_amount());
            else if (sends_position_updates())
            {
                delta = last_sent_local_position - transform.localPosition;
                if (delta.magnitude > position_resolution())
                    client.send_position_update(this);
            }
        }
    }

    public static networked_v2 look_up(string path)
    {
        var found = Resources.Load<networked_v2>(path);
        if (found == null) throw new System.Exception("Could not find the prefab " + path);
        return found;
    }

    private void OnDrawGizmos()
    {
        //Gizmos.matrix = transform.parent == null ? Matrix4x4.identity : 
        //     transform.parent.localToWorldMatrix;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(last_sent_local_position, position_resolution());
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target_local_position, position_resolution());
    }

    /// <summary> My unique id on the network. Negative values are unique only on this
    /// client and indicate that I'm awating a network-wide id. </summary>
    public int network_id
    {
        get => _network_id;
        set
        {
            objects.Remove(_network_id);

            if (objects.ContainsKey(value))
                throw new System.Exception("Tried to overwrite network id!");

            objects[value] = this;
            _network_id = value;
        }
    }
    int _network_id;

    /// <summary> Forget the netowrk object on this client. The object
    /// remains on the server + potentially on other clients. </summary>
    public void forget()
    {
        network_utils.top_down(this, (nw) => objects.Remove(nw.network_id));
        Destroy(gameObject);
    }

    public void delete()
    {
        if (network_id < 0)
        {
            // If unregistered, try again until
            // registered.
            Invoke("delete", 0.1f);
            return;
        }

        client.on_delete(this);
        network_utils.top_down(this, (nw) => objects.Remove(nw.network_id));
        Destroy(gameObject);
    }

    //################//
    // STATIC METHODS //
    //################//

    /// <summary> The objects on this client, keyed by their network id. </summary>
    static Dictionary<int, networked_v2> objects = new Dictionary<int, networked_v2>();

    /// <summary> Return the object with the given network id. </summary>
    public static networked_v2 find_by_id(int id) { return objects[id]; }

    public static void network_updates()
    {
        // Update network objects
        foreach (var kv in objects)
        {
            int id = kv.Key;
            var nw = kv.Value;

            if (nw == null)
            {
                // The networked object was destroyed, but not removed from
                // the dictionary, throw an error.
                string err = "Netowrk object not destroyed correctly. " +
                             "You should not call Destroy() on a networked object. " +
                             "Use networked.forget() or networked.delete() instead.";
                throw new System.Exception(err);
            }

            nw.network_update();
        }
    }

    public static string objects_info()
    {
        return objects.Count + " objects.";
    }

#if UNITY_EDITOR

    // The custom editor for networked types
    [UnityEditor.CustomEditor(typeof(networked_v2), true)]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var nw = (networked_v2)target;
            UnityEditor.EditorGUILayout.IntField("Network ID", nw.network_id);
        }
    }

#endif
}

public class networked_player : networked_v2
{
    /// <summary> How far can the player see other networked objects? </summary>
    public float render_range
    {
        get => _render_range;
        set
        {
            if (_render_range == value)
                return; // No change

            _render_range = value;
            client.on_render_range_change(this);
        }
    }
    float _render_range = server.INIT_RENDER_RANGE;
}




//########//
// CLIENT //
//########//



public static class client
{
    static int last_local_id = 0;

    static TcpClient tcp;

    static network_utils.traffic_monitor traffic_up;
    static network_utils.traffic_monitor traffic_down;

    public static networked_v2 create(Vector3 position,
        string local_prefab, string remote_prefab = null,
        networked_v2 parent = null, Quaternion rotation = default)
    {
        // If remote prefab not specified, it is the same
        // as the local prefab.
        if (remote_prefab == null)
            remote_prefab = local_prefab;

        // Instantiate the local object, but keep the name
        var created = networked_v2.look_up(local_prefab);
        string name = created.name;
        created = Object.Instantiate(created);
        created.name = name;

        // Create the object with the desired position + rotation
        if (rotation.Equals(default)) rotation = Quaternion.identity;
        created.networked_position = position;
        created.transform.rotation = rotation;
        created.on_position_up_to_date();

        // Parent if requested
        if (parent != null)
            created.transform.SetParent(parent.transform);

        // Assign a (negative) unique local id
        created.network_id = --last_local_id;

        // Get the id of my parent
        int parent_id = 0;
        if (parent != null) parent_id = parent.network_id;
        if (parent_id < 0) throw new System.Exception("Cannot create children of unregistered objects!");

        // Request creation on the server
        message_senders[MESSAGE.CREATE](local_prefab, remote_prefab,
            created.transform.localPosition, parent_id, created.network_id);

        return created;
    }

    /// <summary> A message waiting to be sent. </summary>
    struct pending_message
    {
        public byte[] bytes;
        public float time_sent;
    }

    /// <summary> Messages to be sent to the server. </summary>
    static Queue<pending_message> message_queue = new Queue<pending_message>();

    /// <summary> Send all of the messages currently queued. </summary>
    static void send_queued_messages()
    {
        // The buffer used to send messages
        byte[] send_buffer = new byte[tcp.SendBufferSize];
        int offset = 0;
        var stream = tcp.GetStream();

        // Send the message queue
        while (message_queue.Count > 0)
        {
            var msg = message_queue.Dequeue();

            if (msg.bytes.Length > tcp.SendBufferSize)
                throw new System.Exception("Message too large!");

            if (offset + msg.bytes.Length > send_buffer.Length)
            {
                // Message would overrun buffer, send the
                // buffer and obtain a new one
                traffic_up.log_bytes(offset);
                stream.Write(send_buffer, 0, offset);
                send_buffer = new byte[tcp.SendBufferSize];
                offset = 0;
            }

            // Copy the message into the send buffer
            System.Buffer.BlockCopy(msg.bytes, 0, send_buffer, offset, msg.bytes.Length);
            offset += msg.bytes.Length; // Move to next message
        }

        // Send the buffer
        if (offset > 0)
        {
            traffic_up.log_bytes(offset);
            stream.Write(send_buffer, 0, offset);
        }
    }

    /// <summary> Called when the render range of a player changes. </summary>
    public static void on_render_range_change(networked_player player)
    {
        message_senders[MESSAGE.RENDER_RANGE_UPDATE](player);
    }

    /// <summary> Send a position update for the given networked object. </summary>
    public static void send_position_update(networked_v2 nw)
    {
        message_senders[MESSAGE.POSITION_UPDATE](nw);
    }

    /// <summary> Called when an object is deleted on this client, sends the server that info. </summary>
    public static void on_delete(networked_v2 deleted)
    {
        message_senders[MESSAGE.DELETE](deleted.network_id);
    }

    /// <summary> Create an object as instructred by a server message, stored in the given buffer. </summary>
    static void create_from_network(byte[] buffer, int offset, int length, bool local)
    {
        int network_id = network_utils.decode_int(buffer, ref offset);
        int parent_id = network_utils.decode_int(buffer, ref offset);
        Vector3 local_position = network_utils.decode_vector3(buffer, ref offset);
        string local_prefab = network_utils.decode_string(buffer, ref offset);
        string remote_prefab = network_utils.decode_string(buffer, ref offset);

        var nw = networked_v2.look_up(local ? local_prefab : remote_prefab);
        string name = nw.name;
        nw = Object.Instantiate(nw);

        networked_v2 parent = parent_id > 0 ? networked_v2.find_by_id(parent_id) : null;

        if (parent == null)
        {
            nw.networked_position = local_position;
        }
        else
        {
            nw.transform.SetParent(parent.transform);
            nw.transform.localPosition = local_position;
            nw.networked_position = nw.transform.position;
        }

        nw.name = name;
        nw.on_position_up_to_date();
        nw.network_id = network_id;
    }

    /// <summary> Connect the client to a server. </summary>
    public static void connect(string host, int port, string username, string password)
    {
        // Connect the TCP client + initialize buffers
        tcp = new TcpClient(host, port);

        traffic_up = new network_utils.traffic_monitor();
        traffic_down = new network_utils.traffic_monitor();

        // Setup message parsers
        message_parsers = new Dictionary<server.MESSAGE, message_parser>
        {
            [server.MESSAGE.CREATE_LOCAL] = (buffer, offset, length) =>
                create_from_network(buffer, offset, length, true),

            [server.MESSAGE.CREATE_REMOTE] = (buffer, offset, length) =>
                create_from_network(buffer, offset, length, false),

            [server.MESSAGE.POSITION_UPDATE] = (buffer, offset, length) =>
            {
                // Update the position of a network object
                int id = network_utils.decode_int(buffer, ref offset);
                Vector3 local_position = network_utils.decode_vector3(buffer, ref offset);
                networked_v2.find_by_id(id).recive_position_update(local_position);
            },

            [server.MESSAGE.UNLOAD] = (buffer, offset, length) =>
            {
                // Remove the object from the client
                int id = network_utils.decode_int(buffer, ref offset);
                networked_v2.find_by_id(id).forget();
            },

            [server.MESSAGE.CREATION_SUCCESS] = (buffer, offset, length) =>
            {
                // Update the local id to a network-wide one
                int local_id = network_utils.decode_int(buffer, ref offset);
                int network_id = network_utils.decode_int(buffer, ref offset);
                networked_v2.find_by_id(local_id).network_id = network_id;
            }
        };

        // Send a message type + payload
        void send(MESSAGE msg_type, byte[] payload)
        {
            // Message is of form [length, type, payload]
            byte[] to_send = network_utils.concat_buffers(
                network_utils.encode_int(payload.Length),
                new byte[] { (byte)msg_type },
                payload
            );

            message_queue.Enqueue(new pending_message
            {
                bytes = to_send,
                time_sent = Time.realtimeSinceStartup
            });
        }

        // Setup message senders
        message_senders = new Dictionary<MESSAGE, message_sender>
        {
            [MESSAGE.LOGIN] = (args) =>
            {
                if (args.Length != 2)
                    throw new System.ArgumentException("Wrong number of arguments!");

                string uname = (string)args[0];
                string pword = (string)args[1];

                var hasher = System.Security.Cryptography.SHA256.Create();
                var hashed = hasher.ComputeHash(System.Text.Encoding.ASCII.GetBytes(pword));

                // Send the username + hashed password to the server
                send(MESSAGE.LOGIN, network_utils.concat_buffers(
                    network_utils.encode_string(uname), hashed));
            },

            [MESSAGE.DISCONNECT] = (args) =>
            {
                if (args.Length != 0)
                    throw new System.ArgumentException("Wrong number of arguments!");

                send(MESSAGE.DISCONNECT, new byte[] { });
            },

            [MESSAGE.CREATE] = (args) =>
            {
                if (args.Length != 5)
                    throw new System.ArgumentException("Wrong number of arguments!");

                string local_prefab = (string)args[0];
                string remote_prefab = (string)args[1];
                Vector3 local_position = (Vector3)args[2];
                int parent_id = (int)args[3];
                int local_id = (int)args[4];

                send(MESSAGE.CREATE, network_utils.concat_buffers(
                    network_utils.encode_string(local_prefab),
                    network_utils.encode_string(remote_prefab),
                    network_utils.encode_vector3(local_position),
                    network_utils.encode_int(parent_id),
                    network_utils.encode_int(local_id)
                ));
            },

            [MESSAGE.DELETE] = (args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                if (network_id < 0)
                    throw new System.Exception("Tried to delete an unregistered object!");

                send(MESSAGE.DELETE, network_utils.encode_int(network_id));
            },

            [MESSAGE.POSITION_UPDATE] = (args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                var nw = (networked_v2)args[0];

                // Send the id + position to the server
                send(MESSAGE.POSITION_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_int(nw.network_id),
                    network_utils.encode_vector3(nw.transform.localPosition)
                ));

                nw.on_position_up_to_date();
            },

            [MESSAGE.RENDER_RANGE_UPDATE] = (args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                var nw = (networked_player)args[0];

                // Send the new render range
                send(MESSAGE.RENDER_RANGE_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_float(nw.render_range)
                ));
            }
        };

        // Send login message
        message_senders[MESSAGE.LOGIN](username, password);
    }

    public static void disconnect()
    {
        if (tcp == null) return;

        // Send a disconnect message, and any
        // queued messages
        message_senders[MESSAGE.DISCONNECT]();
        send_queued_messages();
        tcp.LingerState = new LingerOption(true, 2);

        // Close the stream
        tcp.GetStream().Close();
        tcp.Close();
    }

    public static void update()
    {
        if (tcp == null) return;

        // Get the tcp stream
        var stream = tcp.GetStream();
        int offset = 0;

        // Read messages to the client
        while (stream.DataAvailable)
        {
            byte[] buffer = new byte[tcp.ReceiveBufferSize];
            int bytes_read = stream.Read(buffer, 0, buffer.Length);
            traffic_down.log_bytes(bytes_read);

            offset = 0;
            while (offset < bytes_read)
            {
                // Parse payload length
                int payload_length = network_utils.decode_int(buffer, ref offset);

                // Parse message type
                var msg_type = (server.MESSAGE)buffer[offset];
                offset += 1;

                // Handle the message
                message_parsers[msg_type](buffer, offset, payload_length);
                offset += payload_length;
            }
        }

        // Run networked object updates
        networked_v2.network_updates();

        // Send messages
        send_queued_messages();
    }

    public static string info()
    {
        if (tcp == null) return "Client not connected.";
        return "Client connected\n" +
               networked_v2.objects_info() + "\n" +
               "Traffic:\n" +
               "    " + traffic_up.usage() + " up\n" +
               "    " + traffic_down.usage() + " down";

    }

    public enum MESSAGE : byte
    {
        // Numbering starts at 1 so erroneous 0's are caught
        LOGIN = 1,           // Client has logged in
        DISCONNECT,          // Disconnect this client
        CREATE,              // Create an object on the server
        DELETE,              // Delete an object from the server
        POSITION_UPDATE,     // Object position needs updating
        RENDER_RANGE_UPDATE, // Client render range has changed
    }

    delegate void message_sender(params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;

    delegate void message_parser(byte[] message, int offset, int length);
    static Dictionary<server.MESSAGE, message_parser> message_parsers;
}



//########//
// SERVER //
//########//



public static class server
{
    /// <summary> A client connected to the server. </summary>
    class client
    {
        // The username + password of this client
        public string username { get; private set; }
        public byte[] password { get; private set; }
        public representation player { get; private set; }

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
                // Load failed - create the player
                player = representation.create(
                    null, player_spawn,
                    player_prefab_local, player_prefab_remote);

                player_representations[username] = player;
            }

            this.username = username;
            this.password = password;
            this.player = player;

            player.transform.SetParent(active_representations);

            load(player, true, false);
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
            if (player == null)
                return false;

            return (rep.transform.position - player.transform.position).magnitude <
                rep.radius + render_range;
        }

        public void update_loaded()
        {
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
            network_utils.top_down(rep, (loading) =>
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
            network_utils.top_down(rep, (unloading) =>
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
        // Needed for proximity tests
        public float radius { get; private set; }

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
                radius = networked_v2.look_up(value).network_radius();
            }
        }
        string _local_prefab;

        // The prefab to create on new remote clients
        public string remote_prefab { get; private set; }

        // Recive a local position update from the given client
        public void position_update(Vector3 new_local_position, client from)
        {
            transform.localPosition = new_local_position;

            // Send position updates to all other clients that have this object
            foreach (var c in connected_clients)
                if (c != from && c.has_loaded(this))
                    message_senders[MESSAGE.POSITION_UPDATE](c, this);
        }

        /// <summary> Serialize this representation into a form that can 
        /// be sent over the network, or saved to disk. </summary>
        public byte[] serialize()
        {
            // Parent_id = 0 if I am not a child of another networked_v2
            representation parent = transform.parent.GetComponent<representation>();
            int parent_id = parent == null ? 0 : parent.network_id;

            if (parent_id < 0)
                throw new System.Exception("Tried to set unregistered parent!");

            return network_utils.concat_buffers(
                network_utils.encode_int(network_id),
                network_utils.encode_int(parent_id),
                network_utils.encode_vector3(transform.localPosition),
                network_utils.encode_string(local_prefab),
                network_utils.encode_string(remote_prefab)
            );
        }

        /// <summary>  Create a network representation. This does not load the
        /// representation on any clients, or send creation messages. </summary>
        public static representation create(
            representation parent, Vector3 local_position,
            string local_prefab, string remote_prefab)
        {
            representation rep = new GameObject(local_prefab).AddComponent<representation>();

            if (parent == null) rep.transform.SetParent(active_representations);
            else rep.transform.SetParent(parent.transform);

            rep.local_prefab = local_prefab;
            rep.remote_prefab = remote_prefab;
            rep.transform.localPosition = local_position;
            rep.network_id = ++last_network_id_assigned; // Network id's start at 1

            return rep;
        }
        static int last_network_id_assigned = 0;

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

    /// <summary> The TCP listener the server is listening with. </summary>
    static TcpListener tcp;

    // Information about how to create new players
    static string player_prefab_local;
    static string player_prefab_remote;
    static Vector3 player_spawn;

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
        string player_prefab_local, string player_prefab_remote,
        Vector3 player_spawn)
    {
        server.player_prefab_local = player_prefab_local;
        server.player_prefab_remote = player_prefab_remote;

        if (!networked_v2.look_up(player_prefab_local).GetType().IsSubclassOf(typeof(networked_player)))
            throw new System.Exception("Local player object must be a networked_player!");

        tcp = new TcpListener(network_utils.local_ip_address(), port);
        tcp.Start();

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

            [global::client.MESSAGE.POSITION_UPDATE] = (client, bytes, offset, length) =>
            {
                int id = network_utils.decode_int(bytes, ref offset);
                Vector3 local_pos = network_utils.decode_vector3(bytes, ref offset);
                representations[id].position_update(local_pos, client);
            },

            [global::client.MESSAGE.RENDER_RANGE_UPDATE] = (client, bytes, offset, length) =>
            {
                client.render_range = network_utils.decode_float(bytes, ref offset);
            },

            [global::client.MESSAGE.CREATE] = (client, bytes, offset, length) =>
            {
                string local_prefab = network_utils.decode_string(bytes, ref offset);
                string remote_prefab = network_utils.decode_string(bytes, ref offset);
                Vector3 local_pos = network_utils.decode_vector3(bytes, ref offset);
                int parent_id = network_utils.decode_int(bytes, ref offset);
                int local_id = network_utils.decode_int(bytes, ref offset);

                representation parent = parent_id > 0 ? representations[parent_id] : null;
                var rep = representation.create(parent, local_pos, local_prefab, remote_prefab);

                client.load(rep, true, true);

                // If this is a child, load it on all other
                // clients that have the parent.
                if (parent != null)
                    foreach (var c in connected_clients)
                        if (c != client)
                            if (c.has_loaded(parent))
                                c.load(rep, false);

                message_senders[MESSAGE.CREATION_SUCCESS](client, local_id, rep.network_id);
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
                network_utils.top_down(deleting, (rep) => representations.Remove(rep.network_id));
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

            [MESSAGE.UNLOAD] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];

                send(client, MESSAGE.UNLOAD, network_utils.encode_int(network_id));
            },

            [MESSAGE.POSITION_UPDATE] = (client, args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                var rep = (representation)args[0];

                send(client, MESSAGE.POSITION_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_int(rep.network_id),
                    network_utils.encode_vector3(rep.transform.localPosition)
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

    public static void stop()
    {
        if (tcp == null) return;
        tcp.Stop();
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
        UNLOAD,            // Unload an object on a client
        POSITION_UPDATE,   // Send a position update to a client
        CREATION_SUCCESS,  // Send when a creation requested by a client was successful
    }

    delegate void message_parser(client c, byte[] bytes, int offset, int length);
    static Dictionary<global::client.MESSAGE, message_parser> message_parsers;

    delegate void message_sender(client c, params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;
}



//###############//
// NETWORK UTILS //
//###############//



public static class network_utils
{
    /// <summary> Concatinate the given byte arrays into a single byte array. </summary>
    public static byte[] concat_buffers(params byte[][] buffers)
    {
        int tot_length = 0;
        for (int i = 0; i < buffers.Length; ++i)
            tot_length += buffers[i].Length;

        int offset = 0;
        byte[] ret = new byte[tot_length];
        for (int i = 0; i < buffers.Length; ++i)
        {
            System.Buffer.BlockCopy(buffers[i], 0, ret, offset, buffers[i].Length);
            offset += buffers[i].Length;
        }

        return ret;
    }

    /// <summary> Get a string displaying the given bytes. </summary>
    public static string byte_string(byte[] bytes, int offset = 0, int length = -1)
    {
        if (length < 0) length = bytes.Length;
        string ret = "";
        for (int i = 0; i < length; ++i)
            ret += bytes[offset + i] + ", ";
        ret = ret.Substring(0, ret.Length - 2);
        return ret;
    }

    /// <summary> Get the ip address of the local machine, as used by a server. </summary>
    public static System.Net.IPAddress local_ip_address()
    {
        // Find the local ip address to listen on
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        System.Net.IPAddress address = null;
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                address = ip;
                break;
            }

        if (address == null)
            throw new System.Exception("No network adapters found!");

        return address;
    }

    /// <summary> Apply the function <paramref name="f"/> to 
    /// <paramref name="parent"/>, and all T in it's children. 
    /// Guaranteed to carry out in top-down order. </summary>
    public static void top_down<T>(T parent, do_func<T> f)
        where T : MonoBehaviour
    {
        Queue<Transform> to_do = new Queue<Transform>();
        to_do.Enqueue(parent.transform);

        while (to_do.Count > 0)
        {
            var doing_to = to_do.Dequeue();

            foreach (Transform t in doing_to)
                to_do.Enqueue(t);

            var found = doing_to.GetComponent<T>();
            if (found != null) f(found);
        }
    }
    public delegate void do_func<T>(T t);

    /// <summary> Class for monitoring network traffic </summary>
    public class traffic_monitor
    {
        int bytes_since_last;
        float time_last;
        float rate;
        float window_length;

        /// <summary> Create a traffic monitor. </summary>
        public traffic_monitor(float window_length = 0.5f)
        {
            time_last = Time.realtimeSinceStartup;
            this.window_length = window_length;
        }

        /// <summary> Get a string reporting the usage (e.g 124.3 KB/s) </summary>
        public string usage()
        {
            // Update rate if sufficent time has passed
            float time = Time.realtimeSinceStartup;
            if (time - time_last > window_length)
            {
                rate = bytes_since_last / (time - time_last);
                bytes_since_last = 0;
                time_last = time;
            }

            if (rate < 1e3f) return System.Math.Round(rate, 2) + " B/s";
            if (rate < 1e6f) return System.Math.Round(rate / 1e3f, 2) + " KB/s";
            if (rate < 1e9f) return System.Math.Round(rate / 1e6f, 2) + " MB/s";
            if (rate < 1e12f) return System.Math.Round(rate / 1e9f, 2) + " GB/s";
            return "A lot";
        }

        public void log_bytes(int bytes) { bytes_since_last += bytes; }
    }

    /// <summary> Encode a string ready to be sent over the network (including it's length). </summary>
    public static byte[] encode_string(string str)
    {
        byte[] ascii = System.Text.Encoding.ASCII.GetBytes(str);
        if (ascii.Length > byte.MaxValue)
            throw new System.Exception("String too long to encode!");
        return concat_buffers(
            new byte[] { (byte)ascii.Length },
            ascii
        );
    }

    /// <summary> Decode a string encoded using <see cref="encode_string(string)"/>.
    /// <paramref name="offset"/> will be incremented by the number of bytes decoded. </summary>
    public static string decode_string(byte[] buffer, ref int offset)
    {
        int length = buffer[offset];
        string str = System.Text.Encoding.ASCII.GetString(buffer, offset + 1, length);
        offset += length + 1;
        return str;
    }

    /// <summary> Encode a vector3 ready to be sent over the network. </summary>
    public static byte[] encode_vector3(Vector3 v)
    {
        return concat_buffers(
            System.BitConverter.GetBytes(v.x),
            System.BitConverter.GetBytes(v.y),
            System.BitConverter.GetBytes(v.z)
        );
    }

    /// <summary> Decode a vector3 encoded using <see cref="encode_vector3(Vector3)"/>. 
    /// Offset will be incremented by the number of bytes decoded. </summary>
    public static Vector3 decode_vector3(byte[] buffer, ref int offset)
    {
        var vec = new Vector3(
            System.BitConverter.ToSingle(buffer, offset),
            System.BitConverter.ToSingle(buffer, offset + sizeof(float)),
            System.BitConverter.ToSingle(buffer, offset + sizeof(float) * 2)
        );
        offset += 3 * sizeof(float);
        return vec;
    }

    /// <summary> Encode an int ready to be sent over the network. </summary>
    public static byte[] encode_int(int i)
    {
        return System.BitConverter.GetBytes(i);
    }

    /// <summary> Decode an integer that was encoded using <see cref="encode_int(int)"/>.
    /// Offset will be incremented by the number of bytes decoded. </summary>
    public static int decode_int(byte[] buffer, ref int offset)
    {
        int i = System.BitConverter.ToInt32(buffer, offset);
        offset += sizeof(int);
        return i;
    }

    /// <summary> Encode a float ready to be sent over the network. </summary>
    public static byte[] encode_float(float f)
    {
        return System.BitConverter.GetBytes(f);
    }

    /// <summary> Decode a float encoded with <see cref="encode_float(float)"/>.
    /// Increments offset by the number of bytes decoded. </summary>
    public static float decode_float(byte[] buffer, ref int offset)
    {
        float f = System.BitConverter.ToSingle(buffer, offset);
        offset += sizeof(float);
        return f;
    }
}