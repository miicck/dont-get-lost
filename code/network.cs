using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// A MonoBehaviour whose existance is serialized over a network connection. Custom
/// data can also be serialized by overriding <c>serialize()</c> and <c>deserialize()</c>.
/// </summary>
public abstract class networked : MonoBehaviour
{
    /// <summary>
    /// The unique id of this object on the network.
    /// Positive id's are network-wide unique, negative
    /// id's are locally unique and indicate we are
    /// awaiting a network-wide id.
    /// </summary>
    int network_id
    {
        get => _network_id;
        set
        {
            int old_id = _network_id;
            _network_id = value;
            client.on_update_id(this, old_id, _network_id);
        }
    }
    int _network_id;

    /// <summary>
    /// The last local (negative) network id assigned to
    /// a networked object.
    /// </summary>
    static int last_local_id = 0;

    /// <summary>
    /// The parent of this networked object. The resulting hierarchy of
    /// networked objects will be mimicked on the server, to allow 
    /// reconstruction on the client. 
    /// </summary>   
    networked net_parent
    {
        get => _net_parent;
        set
        {
            if (_net_parent != null)
                throw new System.NotImplementedException("Tried to change a networked_monobehaviour's network parent!");

            _net_parent = value ?? throw new System.Exception("Tried to parent a networked_monobehaviour to null!");
            transform.SetParent(_net_parent.transform);
        }
    }
    networked _net_parent;

    /// <summary>
    /// The unique id of the type of this networked object.
    /// </summary>
    int type_id { get => get_networked_type_id(GetType()); }

    /// <summary>
    /// Create a networked object of the given type on the client side. This will
    /// automatically send the neccassary messages to also create the object on the server.
    /// </summary>
    /// <typeparam name="T">Networked type to create.</typeparam>
    /// <param name="parent">Networked parent under which to create.</param>
    /// <returns>A newly created network object of type T.</returns>
    public static T create<T>(networked parent)
        where T : networked
    {
        if (parent.network_id < 0)
            throw new System.Exception("Tried to create a child of an unregistered networked_monobehaviour!");

        var t = new GameObject().AddComponent<T>();
        t.name = t.GetType().Name;

        // Set the parent
        t.net_parent = parent;

        // Assign a unique negative id
        t.network_id = --last_local_id;

        // Client-side initialization
        t.on_create(true);

        // This object is being newly added as a child 
        // of a networked_monobehaviour, this implies it is new
        // to both the client and the server (otherwise
        // it would have been created when it's parent
        // was created).
        client.send_message(t, CLIENT_MSG.CREATE_NEW);

        return t;
    }

    /// <summary>
    /// The last set of serialzied bytes sent to the server.
    /// </summary>
    byte[] last_serialized;

    /// <summary>
    /// Run updates for this networked object.
    /// </summary>
    private void update()
    {
        byte[] serial = serialize();
        if (serial == null) return; // No serialization to do

        // Check if the serialization has 
        // changed since the last one sent
        if (last_serialized != null)
        {
            if (last_serialized.Length != serial.Length)
                throw new System.Exception("Variable length serialization is not supported!");

            bool same = true;
            for (int i = 0; i < serial.Length; ++i)
                if (serial[i] != last_serialized[i])
                {
                    same = false;
                    break;
                }

            if (same) return; // No serialization to do
        }

        // Send the updated serialization to the server
        last_serialized = serial;
        client.send_message(this, CLIENT_MSG.SERIALIZATION_UPDATE);
    }

    /// <summary>
    /// Called when the object is created, but just before any messages are sent to the server.
    /// This allows for initialization, so that the server reccives a suitably initialized object.
    /// </summary>
    /// <param name="creation_args"></param>
    protected virtual void on_create(bool local) { }

    /// <summary>
    /// Called the first time that this object is syncronised with the
    /// version on the server (post syncronisation).
    /// </summary>
    protected virtual void on_first_sync() { }

    /// <summary> 
    /// Serialize object data into bytes.
    /// </summary>
    /// <returns></returns>
    // Virtual, because it is a reasonable use case 
    // for a networked_monobehaviour to simply exist
    // and not contain any serialized infromation
    // (a network_section, for example).
    protected virtual byte[] serialize() { return null; }

    /// <summary>
    /// Deserialize object data from the given bytes
    /// starting at offset and continuing for count bytes.
    /// </summary>
    /// <param name="bytes">Bytes containing serialization.</param>
    /// <param name="offset">Start of serialization within <paramref name="bytes"/>.</param>
    /// <param name="count">Length of serialization within <paramref name="bytes"/>.</param>
    protected virtual void deserialize(byte[] bytes, int offset, int count) { }

#if UNITY_EDITOR
    /// <summary>
    /// Custom editor for a networked object, displaying network info in the inspector.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(networked), true)]
    class custom_editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var nm = (networked)target;
            UnityEditor.EditorGUILayout.IntField("Network id", nm.network_id);
            base.OnInspectorGUI();
        }
    }
#endif



    //#################//
    // NETWORK SECTION //
    //#################//



    /// <summary>
    /// A top-level networked object; all networked objects should be children of 
    /// a <c>networked.section</c>. The existance of a <c>networked.section</c> 
    /// on a client implies that it's children should be serialized to that client.
    /// </summary>
    public abstract class section : networked
    {
        /// <summary>
        /// Bytes used to compare network sections on the server, to check if this
        /// a section is already open on the server, or if it needs creating.
        /// </summary>
        /// <returns></returns>
        public abstract byte[] section_id_bytes();

        public abstract void section_id_initialize(params object[] section_id_init_args);

        /// <summary>
        /// Called when a networked section is created, note that there is no
        /// argument specifying if this was a local creation or not, because
        /// sections cannot be local.
        /// </summary>
        protected virtual void on_create() { }

        /// <summary>
        /// Create a network section on the client. Messages will automatically
        /// be sent to the server to retrieve and build the section if it already exists.
        /// </summary>
        /// <typeparam name="T">The type of section to create</typeparam>
        /// <param name="section_id_init_args"></param>
        /// <returns>
        /// A new section, that will soon be either
        /// be deserializd from the server, or created on the server.
        /// </returns>
        public static T create<T>(params object[] section_id_init_args)
            where T : section
        {
            var t = new GameObject().AddComponent<T>();
            t.name = t.GetType().Name;

            // Assign a unique negative id
            t.network_id = --last_local_id;

            // Carry out client-side initialization
            t.section_id_initialize(section_id_init_args);
            t.on_create();

            // Ask the server to check if this network_section exists
            client.send_message(t, CLIENT_MSG.CHECK_SECTION);

            return t;
        }
    }




    //########//
    // CLIENT //
    //########//



    /// <summary>
    /// Client-side management of networked objects.
    /// </summary>
    public static class client
    {
        /// <summary>
        /// Returns true if the client is connected to a server.
        /// </summary>
        public static bool connected { get { return tcp != null; } }

        /// <summary>
        /// The TCP data stream to/from the server.
        /// </summary>
        static NetworkStream stream;

        /// <summary>
        /// The TCP connection to the server.
        /// </summary>
        static TcpClient tcp;

        /// <summary>
        /// All of the networked monobehaviours on the client, indexed by network id
        /// </summary>
        static Dictionary<int, networked> networked_behaviours = new Dictionary<int, networked>();

        static traffic_monitor sent;
        static traffic_monitor received;

        /// <summary>
        /// The buffer of bytes reccived from the server.
        /// </summary>
        static byte[] buffer;

        /// <summary>
        /// Deserialize an entire network section on the client.
        /// </summary>
        /// <param name="section">The (already existing) section that is being deserialized.</param>
        /// <param name="serialization_offset">The location of the section serialization in the buffer.</param>
        /// <param name="serialization_length">The length of the section serialization in the buffer.</param>
        static void deserialize_tree(section section,
            int serialization_offset, int serialization_length)
        {
            bool first = true;
            int offset = serialization_offset;
            int end = serialization_offset + serialization_length;
            while (offset < end)
            {
                int length = System.BitConverter.ToInt32(buffer, offset);
                int id = System.BitConverter.ToInt32(buffer, offset + sizeof(int));
                int parent_id = System.BitConverter.ToInt32(buffer, offset + sizeof(int) * 2);
                int type_id = System.BitConverter.ToInt32(buffer, offset + sizeof(int) * 3);

                if (first)
                {
                    // The very first serialization reccived from the 
                    // server will is that of the section itself.
                    section.network_id = id;
                    section.deserialize(buffer, offset + sizeof(int) * 4, length - sizeof(int) * 4);
                    section.on_first_sync();
                    first = false;
                }
                else
                {
                    // This is a decendant of the section and so 
                    // needs creating as well as deseriailizing.
                    create_from_network(type_id, id, parent_id, offset + sizeof(int) * 4, length - sizeof(int) * 4);
                }

                offset += length;
            }
        }

        /// <summary>
        /// Create a new networked object, that was initially created on another client,
        /// according to instructions sent by the server.
        /// </summary>
        /// <param name="network_id">Network id of the object to create.</param>
        /// <param name="msg_start">Start of data descibing the object in the buffer.</param>
        /// <param name="msg_length">Length of data describing the object in the buffer.</param>
        static void deserialize_new_creation(int network_id, int msg_start, int msg_length)
        {
            int type_id = System.BitConverter.ToInt32(buffer, msg_start);
            int parent_id = System.BitConverter.ToInt32(buffer, msg_start + sizeof(int));
            int serial_start = msg_start + 2 * sizeof(int);
            int serial_length = msg_length - 2 * sizeof(int);
            create_from_network(type_id, network_id, parent_id, serial_start, serial_length);
        }

        /// <summary>
        /// Create a networked_monobheaviour on a the client, based on
        /// instructions sent by the server.
        /// </summary>
        /// <param name="type_id">The id of the network type to create.</param>
        /// <param name="network_id">The network id of the object to be created.</param>
        /// <param name="parent_id">The id of the parent of the object to be created.</param>
        /// <param name="serialization_offset">The location of the serialization in the buffer.</param>
        /// <param name="serialization_length">The length of the serialization in the buffer.</param>
        static void create_from_network(int type_id, int network_id, int parent_id,
            int serialization_offset, int serialization_length)
        {
            System.Type type = get_networked_type_by_id(type_id);
            var nm = (networked)new GameObject().AddComponent(type);
            nm.name = type.Name;
            nm.network_id = network_id;
            nm.net_parent = networked_behaviours[parent_id];
            nm.on_create(false); // This is not a local object
            nm.last_serialized = new byte[serialization_length];
            System.Buffer.BlockCopy(
                buffer, serialization_offset,
                nm.last_serialized, 0, serialization_length);
            nm.deserialize(nm.last_serialized, 0, serialization_length);
            nm.on_first_sync();
        }

        /// <summary>
        /// Send a message to the server of the given type and payload,
        /// filling in message header information in a standard way.
        /// </summary>
        /// <param name="from">The networked object that the message is about.</param>
        /// <param name="message_type">The type of message being sent.</param>
        /// <param name="payload">The payload to send.</param>
        static void send_payload(networked from, byte message_type, byte[] payload)
        {
            if (!connected)
                throw new System.Exception("Networking client not started!");

            int tot_length = sizeof(int) * 2 + 1 + payload.Length;
            byte[] to_send = concat_buffers(
                System.BitConverter.GetBytes(tot_length),      // Length of message
                new byte[] { message_type },                   // Message type
                System.BitConverter.GetBytes(from.network_id), // Network id
                payload                                        // Payload
            );

            if (to_send.Length != tot_length)
                throw new System.Exception("Check calculation of message length!");

            sent.log_bytes(to_send.Length);
            if (to_send.Length > tcp.SendBufferSize)
                throw new System.Exception("Message too long!");
            stream.Write(to_send, 0, to_send.Length);
        }

        /// <summary>
        /// Send a predefined message type to the server from
        /// the given networked object.
        /// </summary>
        /// <param name="from">The network object that the message is about.</param>
        /// <param name="message_type">The type of message to send.</param>
        public static void send_message(networked from, CLIENT_MSG message_type)
        {
            switch (message_type)
            {
                // Send an upadted serialization
                case CLIENT_MSG.SERIALIZATION_UPDATE:
                    send_payload(from, (byte)CLIENT_MSG.SERIALIZATION_UPDATE, from.last_serialized);
                    break;

                // Ask the server if a section exists
                case CLIENT_MSG.CHECK_SECTION:
                    var sec_from = (section)from;
                    send_payload(sec_from, (byte)CLIENT_MSG.CHECK_SECTION, sec_from.section_id_bytes());
                    break;

                // Request the server make a new object of this type
                case CLIENT_MSG.CREATE_NEW_SECTION:

                    // Send the server my serialization
                    sec_from = (section)from;
                    var sec_id_bytes = sec_from.section_id_bytes();
                    var sec_serial = sec_from.serialize();
                    sec_from.last_serialized = sec_serial;

                    send_payload(sec_from, (byte)CLIENT_MSG.CREATE_NEW_SECTION, concat_buffers(
                        System.BitConverter.GetBytes(sec_from.type_id),    // Type id
                        System.BitConverter.GetBytes(sec_id_bytes.Length), // Length of section id bytes
                        sec_id_bytes,                                      // Section id bytes
                        sec_serial ?? new byte[] { }                       // Serialization
                    ));

                    break;

                // Create a new networked_monobehaviour on the server
                case CLIENT_MSG.CREATE_NEW:

                    // Send the server my serialization
                    var serial = from.serialize();
                    from.last_serialized = serial;

                    send_payload(from, (byte)CLIENT_MSG.CREATE_NEW, concat_buffers(
                        System.BitConverter.GetBytes(from.type_id),               // Type id
                        System.BitConverter.GetBytes(from.net_parent.network_id), // Parent id
                        serial ?? new byte[0]                                     // Serialization
                    ));

                    break;

                default:
                    throw new System.Exception("Unkown send message type: " + message_type + "!");
            }
        }

        /// <summary>
        /// Process a message from the server that is about the networked object with
        /// the specified id.
        /// </summary>
        /// <param name="message_type">The type of message.</param>
        /// <param name="network_id">The id of the networked object to which the message is directed.</param>
        /// <param name="msg_offset">The location of the message in the buffer.</param>
        /// <param name="msg_length">The length of the message in the buffer.</param>
        static void process_message(SERVER_MSG message_type, int network_id, int msg_offset, int msg_length)
        {
            switch (message_type)
            {
                case SERVER_MSG.SERIALIZATION_UPDATE:
                    var nb = networked_behaviours[network_id];
                    System.Buffer.BlockCopy(buffer, msg_offset, nb.last_serialized, 0, msg_length);
                    nb.deserialize(nb.last_serialized, 0, msg_length);
                    break;

                case SERVER_MSG.NEW_CREATION:
                    deserialize_new_creation(network_id, msg_offset, msg_length);
                    break;

                case SERVER_MSG.SECTION_DOESNT_EXIST:
                    send_message(networked_behaviours[network_id], CLIENT_MSG.CREATE_NEW_SECTION);
                    break;

                case SERVER_MSG.SECTION_EXISTS:
                    deserialize_tree((section)networked_behaviours[network_id], msg_offset, msg_length);
                    break;

                case SERVER_MSG.SECTION_CREATION_SUCCESS:
                    var section_created = networked_behaviours[network_id];
                    section_created.network_id = System.BitConverter.ToInt32(buffer, msg_offset);
                    section_created.on_first_sync(); // We've synced with the server for the first time
                    break;

                case SERVER_MSG.CREATION_SUCCESS:
                    var object_created = networked_behaviours[network_id];
                    object_created.network_id = System.BitConverter.ToInt32(buffer, msg_offset);
                    object_created.on_first_sync(); // We've synced with the server for the first time
                    break;

                default:
                    throw new System.Exception("Unkown message type: " + message_type);
            }
        }

        /// <summary>
        /// Process the first <paramref name="count"/> bytes of the buffer.
        /// </summary>
        /// <param name="count">Number of bytes to process.</param>
        static void process_buffer(int count)
        {
            int offset = 0;
            while (offset < count)
            {
                // Get the length of this message (including the message type and the length bytes)
                int message_length = System.BitConverter.ToInt32(buffer, offset);

                // Get the messge type
                SERVER_MSG msg_type = (SERVER_MSG)buffer[offset + sizeof(int)];

                // Get the network id
                int network_id = System.BitConverter.ToInt32(buffer, offset + sizeof(int) + 1);

                // Process just the message part
                int header_length = 2 * sizeof(int) + 1;
                process_message(msg_type, network_id,
                    offset + header_length, message_length - header_length);

                // Shift to next message
                offset += message_length;
                if (offset > count)
                    throw new System.Exception("Message overrun!");
            }
        }

        /// <summary>
        /// Connect to a server at the given hostname and port.
        /// </summary>
        /// <param name="host">Hostname of server.</param>
        /// <param name="port">Port to connect through.</param>
        public static void connect_to_server(string host, int port)
        {
            tcp = new TcpClient(host, port);
            stream = tcp.GetStream();
            buffer = new byte[tcp.ReceiveBufferSize];
            sent = new traffic_monitor(Time.realtimeSinceStartup);
            received = new traffic_monitor(Time.realtimeSinceStartup);
        }

        /// <summary>
        /// Disconnect from the server (if connected).
        /// </summary>
        public static void disconnect()
        {
            tcp.Close();
            tcp = null;
        }

        /// <summary>
        /// Called when a networked behaviour's id changes.
        /// </summary>
        /// <param name="nw">The behaviour whose id is changing.</param>
        /// <param name="old_id">The id of this behaviour before the change.</param>
        /// <param name="new_id">The id of this behaviour after the change.</param>
        public static void on_update_id(networked nw, int old_id, int new_id)
        {
            // Update the networked behaviours dictionary
            networked_behaviours.Remove(old_id);

            if (networked_behaviours.ContainsKey(new_id))
                throw new System.Exception("networked behaviour with this id already exists!");

            networked_behaviours[new_id] = nw;
        }

        /// <summary>
        /// Deal with replies from the server, and update networked
        /// objects correspondingly.
        /// </summary>
        public static void update()
        {
            if (!connected) return;

            while (stream.DataAvailable)
            {
                int bytes_read = stream.Read(buffer, 0, buffer.Length);
                if (bytes_read == buffer.Length)
                    throw new System.Exception("Buffer too small!");

                received.log_bytes(bytes_read);
                process_buffer(bytes_read);
            }

            foreach (var kv in networked_behaviours)
                kv.Value.update();

            sent.log_time(Time.realtimeSinceStartup);
            received.log_time(Time.realtimeSinceStartup);
        }

        public static string info()
        {
            if (!connected) return "Not connected.";
            string inf = "Client connected\n";
            inf += "Traffic:\n    " + sent.usage() + " up\n    " + received.usage() + " down\n";
            inf += "Objects: " + networked_behaviours.Count;
            return inf;
        }
    }



    //########//
    // SERVER //
    //########//



    /// <summary>
    /// Server-side management of networked objects.
    /// </summary>
    public static class server
    {
        // A client connected to the server
        class client
        {
            public TcpClient tcp { get; private set; }
            public NetworkStream stream { get; private set; }

            public client(TcpClient client)
            {
                tcp = client;
                stream = client.GetStream();
            }
        }

        /// <summary>
        /// Returns true if the server has been started.
        /// </summary>
        public static bool started { get => tcp != null; }

        /// <summary>
        /// Returns the port that the server is listening
        /// on, if it is listening.
        /// </summary>
        public static int port { get; private set; }

        /// <summary>
        /// The tcp listener that the server is listening on.
        /// </summary>
        static TcpListener tcp;

        /// <summary>
        /// The clients currenlty connected to the server.
        /// </summary>
        static HashSet<client> clients = new HashSet<client>();

        /// <summary>
        /// Server-side representations of networked objects.
        /// </summary>
        static Dictionary<int, representation> representations
            = new Dictionary<int, representation>();

        /// <summary>
        /// Server-side representations of network sections.
        /// </summary>
        static Dictionary<int, section_representation> section_representations
            = new Dictionary<int, section_representation>();

        /// <summary>
        /// Buffer containing messages from clients.
        /// </summary>
        static byte[] buffer;

        static traffic_monitor sent;
        static traffic_monitor received;

        /// <summary>
        /// Called when a client has been found to have disconnected.
        /// </summary>
        /// <param name="c"></param>
        static void on_disonnect(client c)
        {
            // Remove the client from all records
            clients.Remove(c);
            foreach (var kv in section_representations)
                kv.Value.remove_client(c);

            // Close the connection
            c.stream.Close();
            c.tcp.Close();
        }

        /// <summary>
        /// Get a section_representation from the section id 
        /// bytes stored at the given location in the buffer.
        /// </summary>
        /// <param name="sec_id_bytes_start">The location of the section id in the buffer.</param>
        /// <param name="sec_id_bytes_count">The legnth of the section id in the buffer.</param>
        /// <returns>The representation if found, otherwise null.</returns>
        static section_representation find_section(int sec_id_bytes_start, int sec_id_bytes_count)
        {
            foreach (var kv in section_representations)
            {
                var r = kv.Value;
                if (r.section_id_bytes.Length != sec_id_bytes_count)
                    continue;

                bool same = true;
                for (int i = 0; i < sec_id_bytes_count; ++i)
                    if (r.section_id_bytes[i] != buffer[sec_id_bytes_start + i])
                    {
                        same = false;
                        break;
                    }

                if (same) return r;
            }
            return null;
        }

        /// <summary>
        /// Send a message to the given client.
        /// </summary>
        /// <param name="client">Client to send the message to.</param>
        /// <param name="mgs_type">The type of message to send.</param>
        /// <param name="network_id">The network id of the object the message is about.</param>
        /// <param name="payload">The payload to send.</param>
        static void send_payload(client client,
            byte mgs_type, int network_id, byte[] payload)
        {
            int msg_length = sizeof(int) * 2 + 1 + payload.Length;
            byte[] to_send = concat_buffers(
                    System.BitConverter.GetBytes(msg_length), // Length of message
                    new byte[] { mgs_type },                  // Message type
                    System.BitConverter.GetBytes(network_id),   // Network id
                    payload                                     // Payload
            );

            if (to_send.Length != msg_length)
                throw new System.Exception("Check calculation of message length!");

            sent.log_bytes(to_send.Length);
            try
            {
                if (to_send.Length > client.tcp.SendBufferSize)
                    throw new System.Exception("Message too long!");
                client.stream.Write(to_send, 0, to_send.Length);
            }
            catch
            {
                on_disonnect(client);
            }
        }

        /// <summary>
        /// Proccess a message within the buffer.
        /// </summary>
        /// <param name="client">The client the message is from.</param>
        /// <param name="message_type">The message type received.</param>
        /// <param name="network_id">The id of the object the message is about.</param>
        /// <param name="message_offset">The location of the message in the buffer.</param>
        /// <param name="message_length">The length of the message in the buffer.</param>
        static void process_message(client client,
            CLIENT_MSG message_type, int network_id, int message_offset, int message_length)
        {
            switch (message_type)
            {
                // Update the serialization of the given networked objects representation
                case CLIENT_MSG.SERIALIZATION_UPDATE:

                    var rep = representations[network_id];
                    System.Buffer.BlockCopy(buffer, message_offset, rep.serialization, 0, message_length);

                    foreach (var c in rep.connected_clients())
                        if (c != client)
                            send_payload(c, (byte)SERVER_MSG.SERIALIZATION_UPDATE, network_id, rep.serialization);

                    break;

                // Check if a section with the sent section_id_bytes exists
                case CLIENT_MSG.CHECK_SECTION:
                    if (network_id >= 0)
                        throw new System.Exception("Should not be checking for registered sections!");

                    section_representation found = find_section(message_offset, message_length);
                    if (found == null)
                    {
                        // No section found, let the client know it's safe to go ahead and create it
                        send_payload(client, (byte)SERVER_MSG.SECTION_DOESNT_EXIST, network_id, new byte[] { });
                    }
                    else
                    {
                        // Section exists, record the fact that this client can see the section
                        // and send the client the neccassary information to reconstruct it.
                        found.add_client(client);
                        send_payload(client, (byte)SERVER_MSG.SECTION_EXISTS, network_id, concat_buffers(
                            found.tree_serialization()
                        ));
                    }
                    break;

                // Create a new section
                case CLIENT_MSG.CREATE_NEW_SECTION:

                    if (network_id >= 0)
                        throw new System.Exception("Registered objects should not send CREATE_NEW messages!");

                    // Parse the type from the message payload
                    System.Type type = get_networked_type_by_id(
                        System.BitConverter.ToInt32(buffer, message_offset));

                    // The length of the section id bytes
                    int sec_id_bytes_length = System.BitConverter.ToInt32(buffer, message_offset + sizeof(int));

                    // Get the section id bytes
                    byte[] sec_id_bytes = new byte[sec_id_bytes_length];
                    System.Buffer.BlockCopy(buffer, message_offset + 2 * sizeof(int),
                        sec_id_bytes, 0, sec_id_bytes_length);

                    // Get hte initial serialization
                    byte[] init_serialization = new byte[message_length - sec_id_bytes_length - 2 * sizeof(int)];
                    System.Buffer.BlockCopy(buffer, message_offset + 2 * sizeof(int) + sec_id_bytes_length,
                        init_serialization, 0, init_serialization.Length);

                    // Create the section representation
                    var tl_rep = section_representation.create(client, type, init_serialization, sec_id_bytes);

                    // Reply with the newly-created id
                    send_payload(client, (byte)SERVER_MSG.SECTION_CREATION_SUCCESS, network_id, concat_buffers(
                        System.BitConverter.GetBytes(tl_rep.id)
                    ));

                    break;

                // Create a new networked object
                case CLIENT_MSG.CREATE_NEW:

                    if (network_id >= 0)
                        throw new System.Exception("Registered objects should not send CREATE_NEW messages!");

                    // Parse the type id from the message payload
                    int type_id = System.BitConverter.ToInt32(buffer, message_offset);

                    // Parse the parent id from the message payload
                    int parent_id = System.BitConverter.ToInt32(buffer, message_offset + sizeof(int));

                    // Parse the initial serialization from the message payload
                    byte[] serialization = new byte[message_length - 2 * sizeof(int)];
                    System.Buffer.BlockCopy(buffer, message_offset + 2 * sizeof(int),
                        serialization, 0, serialization.Length);

                    // Create the representation of this networked object
                    rep = representation.create(type_id, parent_id, serialization);

                    // Reply with the newly-created id (still addressed to the old negative network_id,
                    // because the client has yet to reccive the new id).
                    send_payload(client, (byte)SERVER_MSG.CREATION_SUCCESS, network_id, concat_buffers(
                        System.BitConverter.GetBytes(rep.id)
                    ));

                    // Let other clients know that the object was created (they reccive the new id, as
                    // these clients will create the corresponding object).
                    foreach (var c in rep.connected_clients())
                        if (c != client)
                            send_payload(c, (byte)SERVER_MSG.NEW_CREATION, rep.id, concat_buffers(
                                System.BitConverter.GetBytes(type_id),
                                System.BitConverter.GetBytes(parent_id),
                                serialization
                            ));

                    break;

                default:
                    throw new System.Exception("Unkown message type: " + message_type + "!");
            }
        }

        /// <summary>
        /// Process the first count bytes from the buffer, which were sent by the given client.
        /// </summary>
        /// <param name="client">The client that sent the bytes.</param>
        /// <param name="count">The number of bytes to process.</param>
        static void process_buffer(client client, int count)
        {
            // The offset is the start of the current message
            int offset = 0;
            while (offset < count)
            {
                // Get the length of this message (including the message type and the length bytes)
                int message_length = System.BitConverter.ToInt32(buffer, offset);

                // Get the messge type
                CLIENT_MSG message_type = (CLIENT_MSG)buffer[offset + sizeof(int)];

                // Get the network id
                int network_id = System.BitConverter.ToInt32(buffer, offset + sizeof(int) + 1);

                // Process just the message part
                int header_length = 2 * sizeof(int) + 1;
                process_message(client, message_type, network_id,
                    offset + header_length, message_length - header_length);

                // Shift to next message
                offset += message_length;
                if (offset > count)
                    throw new System.Exception("Message overrun!");
            }
        }

        /// <summary>
        /// Start a server on the local machine.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public static void start(int port)
        {
            if (started) return;
            var address = local_ip_address();

            // Create and start the listener
            tcp = new TcpListener(address, port);
            server.port = port;
            tcp.Start();

            sent = new traffic_monitor(Time.realtimeSinceStartup);
            received = new traffic_monitor(Time.realtimeSinceStartup);
        }

        /// <summary>
        /// Stop the server listening.
        /// </summary>
        public static void stop()
        {
            tcp.Stop();
            tcp = null;
        }

        /// <summary>
        /// Get the ip address of the local machine, as used by a server.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Process messages from clients and send serialization updates.
        /// </summary>
        public static void update()
        {
            if (!started) return;

            // Check for pending connection requests
            while (tcp.Pending())
                clients.Add(new client(tcp.AcceptTcpClient()));

            // Read stream updates (enumerate over new list, so we can modify the connected clients)
            foreach (var c in new List<client>(clients))
            {
                if (buffer == null || buffer.Length < c.tcp.SendBufferSize)
                    buffer = new byte[c.tcp.SendBufferSize];

                if (!c.tcp.Connected) continue;
                while (c.stream.DataAvailable)
                {
                    int bytes_read = c.stream.Read(buffer, 0, buffer.Length);
                    if (bytes_read == buffer.Length)
                        throw new System.Exception("Buffer too small, please implement dynamic resizing!");

                    received.log_bytes(bytes_read);
                    process_buffer(c, bytes_read);
                }
            }

            sent.log_time(Time.realtimeSinceStartup);
            received.log_time(Time.realtimeSinceStartup);
        }

        public static string info()
        {
            if (!started) return "Not running.";

            string inf = "Listening on " + tcp.LocalEndpoint + "\n";
            inf += "Traffic:\n    " + sent.usage() + " up\n    " + received.usage() + " down\n";
            inf += "Clients:" + clients.Count + "\n";
            inf += "Objects: " + representations.Count +
                   " (of which " + section_representations.Count + " are sections)";
            return inf;
        }



        //#################//
        // REPRESENTATIONS //
        //#################//



        /// <summary>
        /// The transform representing the server, which will be the
        /// parent of the section_representations.
        /// </summary>
        static Transform server_representation
        {
            get
            {
                if (_server_representation == null)
                    _server_representation = new GameObject("server").transform;
                return _server_representation;
            }
        }
        static Transform _server_representation;

        /// <summary>
        /// The server-side representation of a networked object.
        /// </summary>
        class representation : MonoBehaviour
        {
            /// <summary>
            /// The network id of the object this represents.
            /// </summary>
            public int id { get; protected set; }
            protected static int last_id = 0;

            /// <summary>
            /// The network id of the parent of the object this represents.
            /// </summary>
            public int parent_id { get; protected set; }

            /// <summary>
            /// The type id of the object this represents.
            /// </summary>
            public int type_id { get; protected set; }

            /// <summary>
            /// The serialization of the object this represents.
            /// </summary>
            public byte[] serialization { get; protected set; }

            /// <summary>
            /// Returns the clients that have need updates about this representation.
            /// </summary>
            /// <returns></returns>
            public virtual List<client> connected_clients()
            {
                // Recurse upwards to the section representation
                // and then return it's clients.
                representation rep = this;
                representation parent;
                while (representations.TryGetValue(rep.parent_id, out parent))
                    rep = parent;
                return rep.connected_clients();
            }

            /// <summary>
            /// Create a representation.
            /// </summary>
            /// <param name="type_id">ID of the type to create.</param>
            /// <param name="parent_id">The parent id of the object to create.</param>
            /// <param name="initial_serialization">The initial serialization of the object.</param>
            /// <returns></returns>
            public static representation create(int type_id, int parent_id, byte[] initial_serialization)
            {
                // Create the representation
                var type = get_networked_type_by_id(type_id);
                var rep = new GameObject(type.Name).AddComponent<representation>();
                rep.transform.SetParent(representations[parent_id].transform);

                // Save the information
                rep.id = ++last_id;
                rep.parent_id = parent_id;
                rep.type_id = type_id;
                representations[rep.id] = rep;
                rep.serialization = initial_serialization;

                return rep;
            }

#if UNITY_EDITOR
            /// <summary>
            /// Custom editor for server representations.
            /// </summary>
            [UnityEditor.CustomEditor(typeof(representation), true)]
            class custom_editor : UnityEditor.Editor
            {
                public override void OnInspectorGUI()
                {
                    var rep = (representation)target;
                    UnityEditor.EditorGUILayout.IntField("Network id", rep.id);
                    base.OnInspectorGUI();
                }
            }
#endif

        }

        /// <summary>
        /// The server-side representation of a network section.
        /// </summary>
        class section_representation : representation
        {
            public static section_representation create(
                client client,
                System.Type type,
                byte[] initial_serialization,
                byte[] section_id_bytes)
            {
                var rep = new GameObject(type.Name).AddComponent<section_representation>();

                rep.id = ++last_id;
                rep.type_id = get_networked_type_id(type);
                rep.serialization = initial_serialization;
                rep.section_id_bytes = section_id_bytes;
                rep.transform.SetParent(server_representation);
                rep.clients = new HashSet<client>() { client };

                representations[rep.id] = rep;
                section_representations[rep.id] = rep;

                return rep;
            }

            /// <summary>
            /// The clients that currently have access to this section.
            /// </summary>
            HashSet<client> clients;

            /// <summary>
            /// Return a copy of the list of connected clients.
            /// </summary>
            /// <returns></returns>
            public override List<client> connected_clients()
            {
                return new List<client>(clients);
            }

            /// <summary>
            /// Register the fact that a client has access to this section.
            /// </summary>
            /// <param name="client">The client to register.</param>
            public void add_client(client client)
            {
                clients.Add(client);
            }

            /// <summary>
            /// Register the fact that a client no longer has access to this section.
            /// </summary>
            /// <param name="client">The client that lost access to this section.</param>
            public void remove_client(client client)
            {
                clients.Remove(client);
            }

            /// <summary>
            /// The identifying bytes of this section.
            /// </summary>
            public byte[] section_id_bytes { get; private set; }

            /// <summary>
            /// Get the serialization of this and all child objects, the
            /// serialization is guaranteed to be in order of depth. The
            /// first serialization is the section representation itself, then
            /// it's children and so on. This is so that, when we deserialize,
            /// the parent for each deserialized object will already have been
            /// deserialized and available to be assigned as the parent.
            /// </summary>
            /// <returns></returns>
            public byte[] tree_serialization()
            {
                List<byte> ser = new List<byte>();

                List<representation> to_serialize = new List<representation> { this };

                while (to_serialize.Count > 0)
                {
                    List<representation> children = new List<representation>();

                    foreach (var r in to_serialize)
                    {
                        // The serialization for this representation, with enough info
                        // to completely reconstruct it client-side
                        int length = sizeof(int) * 4 + r.serialization.Length;
                        ser.AddRange(System.BitConverter.GetBytes(length));      // The length of the serialization
                        ser.AddRange(System.BitConverter.GetBytes(r.id));        // id
                        ser.AddRange(System.BitConverter.GetBytes(r.parent_id)); // parents id
                        ser.AddRange(System.BitConverter.GetBytes(r.type_id));   // type id
                        ser.AddRange(r.serialization);                           // type-specific serialization

                        // Remember the children, they are next up for serialization
                        foreach (Transform t in r.transform)
                        {
                            var c = t.GetComponent<representation>();
                            if (c != null)
                                children.Add(c);
                        }
                    }

                    to_serialize = children;
                }

                return ser.ToArray();
            }
        }
    }



    //###########//
    // UTILITIES //
    //###########//



    /// <summary>
    /// The types of messages that can be sent from a client.
    /// </summary>
    public enum CLIENT_MSG : byte
    {
        // Request a check for section existance
        CHECK_SECTION = 1, // Start numbering at 1, so erroneous 0's error out

        // Request an id for a newly-created object
        CREATE_NEW,
        CREATE_NEW_SECTION,

        // Send a serialization update
        SERIALIZATION_UPDATE,
    }

    /// <summary>
    /// The types of message that can be sent from the server.
    /// </summary>
    public enum SERVER_MSG : byte
    {
        // Replies to query for section existance
        SECTION_EXISTS = 1,  // Start numbering at 1, so erroneous 0's error out
        SECTION_DOESNT_EXIST,

        // Replies to confirm creation of objects
        CREATION_SUCCESS,
        SECTION_CREATION_SUCCESS,

        // Messages to inform clients about the create of objects
        NEW_CREATION,

        // A serialization update needs to be applied
        SERIALIZATION_UPDATE,
    }

    /// <summary>
    /// Class for monitoring network traffic
    /// </summary>
    class traffic_monitor
    {
        int bytes_since_last;
        float time_last;
        float rate;
        float window_length;

        /// <summary>
        /// Create a traffic monitor.
        /// </summary>
        /// <param name="time">Time in seconds created.</param>
        /// <param name="smoothing">Amount to smooth calculated rates.</param>
        public traffic_monitor(float time, float window_length = 0.5f)
        {
            time_last = time;
            this.window_length = window_length;
        }

        /// <summary>
        /// Log a time interval, and calculate the rate
        /// since the last time interval.
        /// </summary>
        /// <param name="time">The time to log.</param>
        public void log_time(float time)
        {
            // Don't update rate unless sufficent time has passed
            if (time - time_last < window_length) return;
            rate = bytes_since_last / (time - time_last);
            bytes_since_last = 0;
            time_last = time;
        }

        /// <summary>
        /// Get a string reporting the usage (e.g 124.3 KB/s)
        /// </summary>
        /// <returns></returns>
        public string usage()
        {
            if (rate < 1e3f) return System.Math.Round(rate, 2) + " B/s";
            if (rate < 1e6f) return System.Math.Round(rate / 1e3f, 2) + " KB/s";
            if (rate < 1e9f) return System.Math.Round(rate / 1e6f, 2) + " MB/s";
            if (rate < 1e12f) return System.Math.Round(rate / 1e9f, 2) + " GB/s";
            return "A lot";
        }

        public void log_bytes(int bytes) { bytes_since_last += bytes; }
    }

    /// <summary>
    /// Concatinate the given byte arrays into a single byte array.
    /// </summary>
    /// <param name="buffers">Arrays to concatinate.</param>
    /// <returns></returns>
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

    static Dictionary<int, System.Type> networked_types_by_id;
    static Dictionary<System.Type, int> networked_ids_by_type;

    /// <summary>
    /// Load the library of networked types, indexed by uniquely defined integers.
    /// </summary>
    static void load_type_ids()
    {
        networked_types_by_id = new Dictionary<int, System.Type>();
        networked_ids_by_type = new Dictionary<System.Type, int>();

        // Get all implementations of networked_object that exist in the assembely
        List<System.Type> types = new List<System.Type>();
        var asm = System.Reflection.Assembly.GetAssembly(typeof(networked));
        foreach (var t in asm.GetTypes())
            if (t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(networked)))
                types.Add(t);

        // Ensure types are in the same order across clients
        types.Sort((t1, t2) => string.Compare(t1.FullName, t2.FullName, false));
        for (int i = 0; i < types.Count; ++i)
        {
            networked_types_by_id[i] = types[i];
            networked_ids_by_type[types[i]] = i;
        }
    }

    /// <summary>
    /// Get the unique id of a particular networked type.
    /// </summary>
    /// <param name="type">The type to retrieve the id of.</param>
    /// <returns></returns>
    public static int get_networked_type_id(System.Type type)
    {
        if (networked_ids_by_type == null) load_type_ids();
        return networked_ids_by_type[type];
    }

    /// <summary>
    /// Get a networked type by it's unique id.
    /// </summary>
    /// <param name="id">The id of the type to retrieve.</param>
    /// <returns></returns>
    public static System.Type get_networked_type_by_id(int id)
    {
        if (networked_types_by_id == null) load_type_ids();
        return networked_types_by_id[id];
    }
}