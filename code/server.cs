using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class server
{
    public static bool started { get => tcp != null; }
    public static System.Net.IPAddress ip { get; private set; }
    public static int port { get; private set; }

    static System.Net.Sockets.TcpListener tcp;

    public static void start(int port)
    {
        var address = local_ip_address();

        // Create and start the listener
        tcp = new System.Net.Sockets.TcpListener(address, port);
        server.ip = address;
        server.port = port;
        tcp.Start();

        Debug.Log("Server started at " + address + ":" + port);
    }

    public static System.Net.IPAddress local_ip_address()
    {
        // Find the local ip address to listen on
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        System.Net.IPAddress address = null;
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                address = ip;
                break;
            }

        if (address == null)
            throw new System.Exception("No network adapters found!");

        return address;
    }

    static System.IO.StreamWriter logfile;
    static void log(string message)
    {
        if (logfile == null)
            logfile = new System.IO.StreamWriter(Application.persistentDataPath + "/server.log");
        logfile.WriteLine(message);
        logfile.Flush();
    }

    static HashSet<System.Net.Sockets.NetworkStream> clients =
        new HashSet<System.Net.Sockets.NetworkStream>();

    // The representations on the server
    static Dictionary<int, representation> representations
        = new Dictionary<int, representation>();

    static Dictionary<int, section_representation> section_representations
        = new Dictionary<int, section_representation>();

    static byte[] buffer = new byte[1024];

    // Get a section_representation from the comparison bytes
    // stored at the given location in the buffer
    static section_representation find_section(int comp_bytes_start, int comp_bytes_length)
    {
        foreach (var kv in section_representations)
        {
            var r = kv.Value;
            if (r.comp_bytes.Length != comp_bytes_length)
                continue;

            bool same = true;
            for (int i = 0; i < comp_bytes_length; ++i)
                if (r.comp_bytes[i] != buffer[comp_bytes_start + i])
                {
                    same = false;
                    break;
                }

            if (same) return r;
        }
        return null;
    }

    static void reply(System.Net.Sockets.NetworkStream client,
        byte reply_type, int network_id, byte[] payload)
    {
        int reply_length = sizeof(int) * 2 + 1 + payload.Length;
        byte[] to_send = utils.concat_buffers(
                System.BitConverter.GetBytes(reply_length), // Length of message
                new byte[] { reply_type },                  // Message type
                System.BitConverter.GetBytes(network_id),   // Network id
                payload                                     // Payload
        );

        if (to_send.Length != reply_length)
            throw new System.Exception("Check calculation of message length!");

        client.Write(to_send, 0, to_send.Length);
    }

    // Proccess a message within the buffer
    static void process_message(System.Net.Sockets.NetworkStream client,
        byte message_type, int network_id, int message_offset, int message_length)
    {
        log(message_type + " : " + network_id + " : " + message_length + " : " + client);

        switch (message_type)
        {
            case message_types.CHECK_SECTION:
                if (network_id >= 0)
                    throw new System.Exception("Should not be checking for registered sections!");

                section_representation found = find_section(message_offset, message_length);
                if (found == null)
                {
                    // No section found, let the client know it's safe to go ahead and create it
                    reply(client, reply_types.SECTION_DOESNT_EXIST, network_id, new byte[] { });
                }
                else
                {
                    // Section exists, send the client the neccassary information to reconstruct it
                    reply(client, reply_types.SECTION_EXISTS, network_id, utils.concat_buffers(
                        found.tree_serialization()
                    ));
                }
                break;

            case message_types.CREATE_NEW_SECTION:

                if (network_id >= 0)
                    throw new System.Exception("Registered objects should not send CREATE_NEW messages!");

                // Parse the type from the message payload
                System.Type type = networked_monobehaviour.get_networked_type_by_id(
                    System.BitConverter.ToInt32(buffer, message_offset));

                // The length of the initial comparision_bytes
                int comp_bytes_length = System.BitConverter.ToInt32(buffer, message_offset + sizeof(int));

                // Get the initial comp_bytes
                byte[] init_comp_bytes = new byte[comp_bytes_length];
                System.Buffer.BlockCopy(buffer, message_offset + 2 * sizeof(int),
                    init_comp_bytes, 0, comp_bytes_length);

                // Get hte initial serialization
                byte[] init_serialization = new byte[message_length - comp_bytes_length - 2 * sizeof(int)];
                System.Buffer.BlockCopy(buffer, message_offset + 2 * sizeof(int) + comp_bytes_length,
                    init_serialization, 0, init_serialization.Length);

                // Create the section representation
                var tl_rep = section_representation.create(type, init_serialization, init_comp_bytes);

                // Reply with the newly-created id
                reply(client, reply_types.SECTION_CREATED, network_id, utils.concat_buffers(
                    System.BitConverter.GetBytes(tl_rep.id)
                ));

                break;

            case message_types.CREATE_NEW:

                if (network_id >= 0)
                    throw new System.Exception("Registered objects should not send CREATE_NEW messages!");

                // Parse the type id from the message payload
                type = networked_monobehaviour.get_networked_type_by_id(
                    System.BitConverter.ToInt32(buffer, message_offset));

                // Parse the parent id from the message payload
                int parent_id = System.BitConverter.ToInt32(buffer, message_offset + sizeof(int));

                // Parse the initial serialization from the message payload
                byte[] serialization = new byte[message_length - 2 * sizeof(int)];
                System.Buffer.BlockCopy(buffer, message_length + 2 * sizeof(int),
                    serialization, 0, serialization.Length);

                // Create the representation of this networked object
                var rep = representation.create(type, parent_id, serialization);

                // Reply with hte newly-created id
                reply(client, reply_types.CREATED, network_id, utils.concat_buffers(
                    System.BitConverter.GetBytes(rep.id)
                ));
                break;

            default:
                throw new System.Exception("Unkown message type: " + message_type + "!");
        }
    }

    // Process count bytes from the buffer, which were sent by the given client
    static void process_buffer(System.Net.Sockets.NetworkStream client, int count)
    {
        // The offset is the start of the current message
        int offset = 0;
        while (offset < count)
        {
            // Get the length of this message (including the message type and the length bytes)
            int message_length = System.BitConverter.ToInt32(buffer, offset);

            // Get the messge type
            byte message_type = buffer[offset + sizeof(int)];

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

    public static void update()
    {
        if (!started) return;

        // Check for pending connection requests
        while (tcp.Pending())
        {
            System.Net.Sockets.TcpClient client = tcp.AcceptTcpClient();
            clients.Add(client.GetStream());
            Debug.Log("Client connected from " + client.Client.LocalEndPoint);
        }

        // Read stream updates
        foreach (var c in clients)
        {
            while (c.DataAvailable)
            {
                int bytes_read = c.Read(buffer, 0, buffer.Length);
                if (bytes_read == buffer.Length)
                    throw new System.Exception("Buffer too small, please implement dynamic resizing!");

                process_buffer(c, bytes_read);

                if (bytes_read == 0)
                    break;
            }
        }
    }

    static Transform _server_representation;
    static Transform server_representation
    {
        get
        {
            if (_server_representation == null)
                _server_representation = new GameObject("server").transform;
            return _server_representation;
        }
    }

    // The server-side representation of a networked_monobehaviour
    public class representation : MonoBehaviour
    {
        protected static int last_id = 0;
        public int id { get; protected set; }
        public int parent_id { get; protected set; }
        public int type_id { get; protected set; }
        public byte[] serialization { get; protected set; }

        public static representation create(System.Type type, int parent_id, byte[] initial_serialization)
        {
            var rep = new GameObject(type.Name).AddComponent<representation>();
            rep.id = ++last_id;
            rep.parent_id = parent_id;
            rep.type_id = networked_monobehaviour.get_networked_type_id(type);
            representations[rep.id] = rep;
            rep.serialization = initial_serialization;
            rep.transform.SetParent(representations[parent_id].transform);
            log("Created representation " + rep.id);
            return rep;
        }

#if UNITY_EDITOR
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

    // The server-side representation of a network_section
    public class section_representation : representation
    {
        public static section_representation create(System.Type type, byte[] initial_serialization,
            byte[] initial_comp_bytes)
        {
            var rep = new GameObject(type.Name).AddComponent<section_representation>();
            rep.id = ++last_id;
            rep.type_id = networked_monobehaviour.get_networked_type_id(type);
            representations[rep.id] = rep;
            section_representations[rep.id] = rep;
            rep.serialization = initial_serialization;
            rep.comp_bytes = initial_comp_bytes;
            rep.transform.SetParent(server_representation);
            log("Created section representation " + rep.id);
            return rep;
        }

        public byte[] comp_bytes { get; private set; }

        // Get the serialization of this and all child objects, the
        // serialization is guaranteed to be in order of depth. The
        // first serialization is the section representation itself, then
        // it's children and so on. This is so that, when we deserialize,
        // the parent for each deserialized object will already have been
        // deserialized and available to be assigned as the parent.
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

    // The types of messages sent to the server
    public static class message_types
    {
        // Request a check for section existance
        public const byte CHECK_SECTION = 101;

        // Request an id for a newly-created object
        public const byte CREATE_NEW = 102;
        public const byte CREATE_NEW_SECTION = 103;
    }

    // The types of reply from the server
    public static class reply_types
    {
        // Replies to query for section existance
        public const byte SECTION_EXISTS = 101;
        public const byte SECTION_DOESNT_EXIST = 102;

        // Replies to confirm creation of objects
        public const byte CREATED = 104;
        public const byte SECTION_CREATED = 103;
    }
}