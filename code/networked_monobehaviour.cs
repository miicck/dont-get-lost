using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class networked_monobehaviour : MonoBehaviour
{
    // The unique id of this object on the network.
    // Positive id's are network-wide unique, negative
    // id's are locally unique and indicate we are
    // awaiting a network-wide id.
    int _network_id;
    public int network_id
    {
        get => _network_id;
        protected set
        {
            log("Updated network id for " + name + " from " + _network_id + " to " + value);

            // Update the networked behaviours dictionary
            networked_behaviours.Remove(_network_id);
            _network_id = value;

            if (networked_behaviours.ContainsKey(_network_id))
                throw new System.Exception("Tried to create a networked_monobehaviour that already exists!");

            networked_behaviours[_network_id] = this;
        }
    }
    protected static int last_local_id = 0;

    // Return the networked_monobehaviour parent
    networked_monobehaviour _net_parent;
    public networked_monobehaviour net_parent
    {
        get => _net_parent;
        private set
        {
            if (_net_parent != null)
                throw new System.NotImplementedException("Tried to change a networked_monobehaviour's network parent!");

            _net_parent = value ?? throw new System.Exception("Tried to parent a networked_monobehaviour to null!");
            transform.SetParent(_net_parent.transform);
        }
    }

    // Create a networked_monobehaviour of the given type on a client.
    public static T create<T>(networked_monobehaviour parent, params object[] args)
        where T : networked_monobehaviour
    {
        if (parent.network_id < 0)
            throw new System.Exception("Tried to create a child of an unregistered networked_monobehaviour!");

        var t = new GameObject().AddComponent<T>();
        t.net_parent = parent;

        // Assign a unique negative id
        t.network_id = --last_local_id;

        // Client-side initialization
        t.on_create(args);

        // This object is being newly added as a child 
        // of a networked_monobehaviour, this implies it is new
        // to both the client and the server (otherwise
        // it would have been created when it's parent
        // was created).
        t.send_message(server.message_types.CREATE_NEW);

        return t;
    }

    // Create a networked_monobheaviour on a the client, based on
    // instructions sent by the server
    static void create_from_network(int type_id, int network_id, int parent_id,
        byte[] buffer, int serialization_offset, int serialization_length)
    {
        System.Type type = get_networked_type_by_id(type_id);
        var nm = (networked_monobehaviour)new GameObject().AddComponent(type);
        nm.network_id = network_id;
        nm.net_parent = networked_behaviours[parent_id];
        nm.deserialize(buffer, serialization_offset, serialization_length);
    }

    protected virtual void send_message(byte message_type)
    {
        switch (message_type)
        {
            case server.message_types.CREATE_NEW:

                send_message(server.message_types.CREATE_NEW, utils.concat_buffers(
                    System.BitConverter.GetBytes(get_networked_type_id(GetType())), // Type id
                    System.BitConverter.GetBytes(net_parent.network_id),            // Parent id
                    serialize() ?? new byte[0]                                      // Serialization
                ));
                break;

            default:
                throw new System.Exception("Unkown send message type: " + message_type + "!");
        }
    }

    // Send a message to the server of the given type and payload,
    // filling in message header information in a standard way.
    protected void send_message(byte message_type, byte[] payload)
    {
        int tot_length = sizeof(int) * 2 + 1 + payload.Length;
        byte[] to_send = utils.concat_buffers(
            System.BitConverter.GetBytes(tot_length),  // Length of message
            new byte[] { message_type },               // Message type
            System.BitConverter.GetBytes(network_id),  // Network id
            payload                                    // Payload
        );

        if (to_send.Length != tot_length)
            throw new System.Exception("Check calculation of message length!");

        tcp.Write(to_send, 0, to_send.Length);
    }

    // Called when the object is created
    protected virtual void on_create(object[] creation_args) { }

    // Serialize this objects properties into bytes.
    // Virtual, because it is a reasonable use case 
    // for a networked_monobehaviour to simply exist
    // and not contain any serialized infromation
    // (a network_section, for example).
    protected virtual byte[] serialize() { return null; }

    // Deserialize this objects properties from the given bytes
    // starting at offset and continuing for count bytes.
    protected virtual void deserialize(byte[] bytes, int offset, int count) { }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(networked_monobehaviour), true)]
    class custom_editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var nm = (networked_monobehaviour)target;
            UnityEditor.EditorGUILayout.IntField("Network id", nm.network_id);
            base.OnInspectorGUI();
        }
    }
#endif

    //################################//
    // NETWORKED MONOBEHAVIOUR CLIENT //
    //################################//

    // All of the networked monobehaviours on the client, indexed by network id
    static Dictionary<int, networked_monobehaviour> networked_behaviours =
         new Dictionary<int, networked_monobehaviour>();

    static Dictionary<int, System.Type> networked_types_by_id;
    static Dictionary<System.Type, int> networked_ids_by_type;

    static void load_type_ids()
    {
        networked_types_by_id = new Dictionary<int, System.Type>();
        networked_ids_by_type = new Dictionary<System.Type, int>();

        // Get all implementations of networked_object that exist in the assembely
        List<System.Type> types = new List<System.Type>();
        var asm = System.Reflection.Assembly.GetAssembly(typeof(networked_monobehaviour));
        foreach (var t in asm.GetTypes())
            if (t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(networked_monobehaviour)))
                types.Add(t);

        // Ensure types are in the same order across clients
        types.Sort((t1, t2) => string.Compare(t1.FullName, t2.FullName, false));
        for (int i = 0; i < types.Count; ++i)
        {
            networked_types_by_id[i] = types[i];
            networked_ids_by_type[types[i]] = i;
        }
    }

    public static int get_networked_type_id(System.Type type)
    {
        if (networked_ids_by_type == null) load_type_ids();
        return networked_ids_by_type[type];
    }

    public static System.Type get_networked_type_by_id(int id)
    {
        if (networked_types_by_id == null) load_type_ids();
        return networked_types_by_id[id];
    }

    public static bool connected { get { return tcp != null; } }

    // The TCP connection to the server
    static System.Net.Sockets.NetworkStream tcp;
    public static void connect_to_server(string host, int port)
    {
        tcp = new System.Net.Sockets.TcpClient(host, port).GetStream();
    }

    static byte[] buffer = new byte[1024];

    static System.IO.StreamWriter logfile;
    static void log(string message)
    {
        if (logfile == null)
        {
            int n = 0;
            string fn = Application.persistentDataPath + "/client.log";
            while (System.IO.File.Exists(fn))
                fn = Application.persistentDataPath + "/client_" + (++n) + ".log";
            logfile = new System.IO.StreamWriter(fn);
        }
        logfile.WriteLine(message);
        logfile.Flush();
    }

    static void deserialize_tree(network_section section,
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
                first = false;
            }
            else
            {
                // This is a decendant of the section and so 
                // needs creating as well as deseriailizing.
                create_from_network(type_id, id, parent_id, buffer, offset + sizeof(int) * 4, length - sizeof(int) * 4);
            }

            offset += length;
        }
    }

    static void process_reply(byte message_type, int network_id, int message_offset, int message_length)
    {
        log(message_type + " : " + network_id + " : " + message_length);

        // Get the networked behaviour this message was intended for
        var nm = networked_behaviours[network_id];

        switch (message_type)
        {
            case server.reply_types.SECTION_DOESNT_EXIST:
                nm.send_message(server.message_types.CREATE_NEW_SECTION);
                break;

            case server.reply_types.SECTION_EXISTS:
                deserialize_tree((network_section)nm, message_offset, message_length);
                break;

            case server.reply_types.SECTION_CREATED:
                nm.network_id = System.BitConverter.ToInt32(buffer, message_offset);
                break;

            case server.reply_types.CREATED:
                nm.network_id = System.BitConverter.ToInt32(buffer, message_offset);
                break;

            default:
                throw new System.Exception("Unkown reply type: " + message_type);
        }
    }

    static void process_buffer(int count)
    {
        int offset = 0;
        while (offset < count)
        {
            // Get the length of this reply (including the message type and the length bytes)
            int message_length = System.BitConverter.ToInt32(buffer, offset);

            // Get the messge type
            byte message_type = buffer[offset + sizeof(int)];

            // Get the network id
            int network_id = System.BitConverter.ToInt32(buffer, offset + sizeof(int) + 1);

            // Process just the reply part
            int header_length = 2 * sizeof(int) + 1;
            process_reply(message_type, network_id,
                offset + header_length, message_length - header_length);

            // Shift to next message
            offset += message_length;
            if (offset > count)
                throw new System.Exception("Message overrun!");
        }
    }

    public static void update()
    {
        if (!connected) return;

        while (tcp.DataAvailable)
        {
            int bytes_read = tcp.Read(buffer, 0, buffer.Length);
            if (bytes_read == buffer.Length)
                throw new System.Exception("Buffer too small, please implement dynamic resizing!");

            process_buffer(bytes_read);

            if (bytes_read == 0)
                break;
        }
    }
}

// All networked_monobehaviours should be children of a network_section. These sections
// can be loaded from the server, along with their children, and are used to define which
// parts of the world should be serialized to this client over the network.
public abstract class network_section : networked_monobehaviour
{
    // Bytes used to compare network_sections on the server, to check if this
    // network section is already open on the server, or if it needs creating.
    protected abstract byte[] comparison_bytes();

    public static T create<T>(params object[] args)
        where T : network_section
    {
        var t = new GameObject().AddComponent<T>();

        // Assign a unique negative id
        t.network_id = --last_local_id;

        // Carry out client-side initialization
        t.on_create(args);

        // Ask the server to check if this network_section exists
        t.send_message(server.message_types.CHECK_SECTION, t.comparison_bytes());

        return t;
    }

    protected override void send_message(byte message_type)
    {
        switch (message_type)
        {
            // Request the server make a new object of this type
            case server.message_types.CREATE_NEW_SECTION:

                // Send the server the id of my parent and my serialization
                var comp_bytes = comparison_bytes();
                send_message(server.message_types.CREATE_NEW_SECTION, utils.concat_buffers(
                    System.BitConverter.GetBytes(get_networked_type_id(GetType())), // Type id
                    System.BitConverter.GetBytes(comp_bytes.Length),                // Length of comparison bytes
                    comparison_bytes(),                                             // Comparision bytes
                    serialize() ?? new byte[] { }                                   // Serialization
                ));
                break;

            default:
                base.send_message(message_type);
                break;
        }
    }
}