using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;

/*
 *  NOTES
 *  - Are we sure we will only need a local/remote prefab, not more versions? 
 *  - How does the server know how to create the player when they first log on?
 */

public class networked_v2 : MonoBehaviour
{
    /// <summary> My width as far as the server is concerned for determining if I can be seen. </summary>
    public virtual float network_radius() { return 1f; }

    public virtual float position_resolution() { return 0.1f; }

    /// <summary> Check if we need to send a position to the server. </summary>
    public bool position_update_required()
    {
        // If we've moved far enough, a position update needs to be sent
        return (transform.position - last_position).magnitude > position_resolution();
    }

    /// <summary> Recive a position update from the server. </summary>
    public void recive_position_update(Vector3 position)
    {
        last_position = position;
        transform.position = position;
    }
    Vector3 last_position;

    public static networked_v2 look_up(string path)
    {
        var found = Resources.Load<networked_v2>(path);
        if (found == null) throw new System.Exception("Could not find the prefab " + path);
        return found;
    }
}

public static class client
{
    /// <summary> Dictionary of networked objects, keyed by network id. </summary>
    static Dictionary<int, networked_v2> objects = new Dictionary<int, networked_v2>();
    static int last_local_id = 0;

    static TcpClient tcp;

    static void create_from_network(byte[] buffer, int offset, int length, bool local)
    {
        int network_id = System.BitConverter.ToInt32(buffer, offset);
        offset += sizeof(int);

        float x = System.BitConverter.ToSingle(buffer, offset);
        offset += sizeof(float);

        float y = System.BitConverter.ToSingle(buffer, offset);
        offset += sizeof(float);

        float z = System.BitConverter.ToSingle(buffer, offset);
        offset += sizeof(float);

        byte local_prefab_length = buffer[offset];
        offset += 1;

        string local_prefab = System.Text.Encoding.ASCII.GetString(buffer, offset, local_prefab_length);
        offset += local_prefab_length;

        byte remote_prefab_length = buffer[offset];
        offset += 1;

        string remote_prefab = System.Text.Encoding.ASCII.GetString(buffer, offset, remote_prefab_length);
        offset += remote_prefab_length;

        var nw = networked_v2.look_up(local ? local_prefab : remote_prefab);
        string name = nw.name;
        nw = Object.Instantiate(nw);
        nw.name = name;

        nw.transform.position = new Vector3(x, y, z);
        objects[network_id] = nw;
    }

    static void send(MESSAGE message_type, byte[] payload)
    {
        // Message is of form [length, type, payload]
        byte[] to_send = network_utils.concat_buffers(
            System.BitConverter.GetBytes(payload.Length),
            new byte[] { (byte)message_type },
            payload
        );

        if (to_send.Length > tcp.SendBufferSize)
            throw new System.Exception("Message too large!");

        tcp.GetStream().Write(to_send, 0, to_send.Length);
    }

    /// <summary> Connect the client to a server. </summary>
    public static void connect(string host, int port, string username, string password)
    {
        // Connect the TCP client + initialize buffers
        tcp = new TcpClient(host, port);

        // Setup the message senders
        message_senders = new Dictionary<MESSAGE, message_sender>
        {
            [MESSAGE.LOGIN] = (args) =>
            {
                byte[] uname = System.Text.Encoding.ASCII.GetBytes((string)args[0]);
                byte[] pword = System.Text.Encoding.ASCII.GetBytes((string)args[1]);

                var hasher = System.Security.Cryptography.SHA256.Create();
                pword = hasher.ComputeHash(pword);

                // Send the username + hashed password to the server
                send(MESSAGE.LOGIN, network_utils.concat_buffers(
                    new byte[] { (byte)uname.Length },
                    uname, pword));
            },

            [MESSAGE.POSITION_UPDATE] = (args) =>
            {
                int id = (int)args[0];
                Vector3 pos = (Vector3)args[1];

                // Send the id + position to the server
                send(MESSAGE.POSITION_UPDATE, network_utils.concat_buffers(
                    System.BitConverter.GetBytes(id),
                    System.BitConverter.GetBytes(pos.x),
                    System.BitConverter.GetBytes(pos.y),
                    System.BitConverter.GetBytes(pos.z)
                ));
            }
        };

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
                int id = System.BitConverter.ToInt32(buffer, offset);
                offset += sizeof(int);

                float x = System.BitConverter.ToSingle(buffer, offset);
                offset += sizeof(float);

                float y = System.BitConverter.ToSingle(buffer, offset);
                offset += sizeof(float);

                float z = System.BitConverter.ToSingle(buffer, offset);
                offset += sizeof(float);

                objects[id].recive_position_update(new Vector3(x, y, z));
            }
        };

        // Send a connection message
        message_senders[MESSAGE.LOGIN](username, password);
    }

    public static void update()
    {
        if (tcp == null) return;

        var stream = tcp.GetStream();
        while (stream.DataAvailable)
        {
            byte[] buffer = new byte[tcp.ReceiveBufferSize];
            int bytes_read = stream.Read(buffer, 0, buffer.Length);

            int offset = 0;
            while (offset < bytes_read)
            {
                // Parse payload length
                int payload_length = System.BitConverter.ToInt32(buffer, offset);
                offset += sizeof(int);

                // Parse message type
                var msg_type = (server.MESSAGE)buffer[offset];
                offset += 1;

                // Handle the message
                message_parsers[msg_type](buffer, offset, payload_length);
                offset += payload_length;
            }
        }

        // Update network objects
        foreach (var kv in objects)
        {
            int id = kv.Key;
            var nw = kv.Value;

            // Send position updates
            if (nw.position_update_required())
            {
                message_senders[MESSAGE.POSITION_UPDATE](id, nw.transform.position);
                nw.recive_position_update(nw.transform.position);
            }
        }
    }

    public enum MESSAGE : byte
    {
        // Numbering starts at 1 so erroneous 0's are caught
        LOGIN = 1,        // Client has logged in
        POSITION_UPDATE,  // Object position needs updating
    }

    delegate void message_sender(params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;

    delegate void message_parser(byte[] message, int offset, int length);
    static Dictionary<server.MESSAGE, message_parser> message_parsers;
}

public static class server
{
    /// <summary> The size of the grid that networked objects are binned into. </summary>
    static float grid_size;

    /// <summary> The TCP listener the server is listening with. </summary>
    static TcpListener tcp;

    // Information about how to create new players
    static string player_prefab_local;
    static string player_prefab_remote;
    static Vector3 player_spawn;

    /// <summary> The clients currently connected to the server </summary>
    static HashSet<client> connected_clients = new HashSet<client>();

    /// <summary> The transform representing the server. </summary>
    static Transform _transform;
    static Transform transform
    {
        get
        {
            if (_transform == null)
                _transform = new GameObject("server").transform;
            return _transform;
        }
    }

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

        // Needed for proximity tests
        public float render_range { get; private set; }

        public client(TcpClient tcp)
        {
            this.tcp = tcp;
            stream = tcp.GetStream();
            render_range = 5f;
        }

        public void login(string username, byte[] password, representation player)
        {
            this.username = username;
            this.password = password;
            this.player = player;
        }
    }

    /// <summary> Represents a networked object on the server. </summary>
    class representation : MonoBehaviour
    {
        // The clients that the object this represents are loaded on
        HashSet<client> loaded_on = new HashSet<client>();

        /// <summary> Load this object on a client, either as a local or remote object. </summary>
        public void load_on(client client, bool local)
        {
            if (local) message_senders[MESSAGE.CREATE_LOCAL](client, serialize());
            else message_senders[MESSAGE.CREATE_REMOTE](client, serialize());
            loaded_on.Add(client);
        }

        /// <summary> Unload the representation on the given client. </summary>
        public void unload_on(client client)
        {
            loaded_on.Remove(client);
            message_senders[MESSAGE.UNLOAD](client, network_id);
        }

        /// <summary> Returns true if this representation is loaded on the given client. </summary>
        public bool loaded(client client) { return loaded_on.Contains(client); }

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

        // Needed for proximity tests
        public float radius { get; private set; }

        // The prefab to create on new clients
        // and the user that first created me
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

        public string remote_prefab { get; private set; }

        public void position_update(Vector3 new_position, client from)
        {
            transform.position = new_position;

            // Send updated position to all the other clients
            foreach (var c in loaded_on)
            {
                if (c == from) continue;
                send(c, MESSAGE.POSITION_UPDATE, network_utils.concat_buffers(
                    System.BitConverter.GetBytes(network_id),
                    System.BitConverter.GetBytes(transform.position.x),
                    System.BitConverter.GetBytes(transform.position.y),
                    System.BitConverter.GetBytes(transform.position.z)
                ));
            }
        }

        public byte[] serialize()
        {
            byte[] local_prefab_bytes = System.Text.Encoding.ASCII.GetBytes(local_prefab);
            byte[] remote_prefab_bytes = System.Text.Encoding.ASCII.GetBytes(remote_prefab);

            return network_utils.concat_buffers(
                System.BitConverter.GetBytes(network_id),
                System.BitConverter.GetBytes(transform.position.x),
                System.BitConverter.GetBytes(transform.position.y),
                System.BitConverter.GetBytes(transform.position.z),
                new byte[] { (byte)local_prefab_bytes.Length },
                local_prefab_bytes,
                new byte[] { (byte)remote_prefab_bytes.Length },
                remote_prefab_bytes
            );
        }

        static int last_network_id_assigned = 0;
        public static representation create(
            representation parent,
            string local_prefab, string remote_prefab,
            Vector3 position)
        {
            representation rep = new GameObject(local_prefab).AddComponent<representation>();

            rep.local_prefab = local_prefab;
            rep.remote_prefab = remote_prefab;
            rep.transform.position = position;
            rep.network_id = ++last_network_id_assigned;

            if (parent == null) rep.transform.SetParent(server.transform);
            else rep.transform.SetParent(parent.transform);

            return rep;
        }
    }

    /// <summary> Representations on the server, keyed by network id. </summary>
    static Dictionary<int, representation> representations = new Dictionary<int, representation>();

    /// <summary> Send the given message type/payload to the given client. </summary>
    static void send(client client, MESSAGE msg_type, byte[] payload)
    {
        byte[] to_send = network_utils.concat_buffers(
            System.BitConverter.GetBytes(payload.Length),
            new byte[] { (byte)msg_type },
            payload
        );

        if (to_send.Length > client.tcp.SendBufferSize)
            throw new System.Exception("Message too large!");

        client.stream.Write(to_send, 0, to_send.Length);
    }

    /// <summary> Start a server listening on the given port on the local machine. </summary>
    public static void start(
        int port, float grid_size, string savename,
        string player_prefab_local, string player_prefab_remote,
        Vector3 player_spawn)
    {
        server.player_prefab_local = player_prefab_local;
        server.player_prefab_remote = player_prefab_remote;

        tcp = new TcpListener(network_utils.local_ip_address(), port);
        tcp.Start();

        // Setup the message senders
        message_parsers = new Dictionary<global::client.MESSAGE, message_parser>
        {
            [global::client.MESSAGE.LOGIN] = (client, bytes, offset, length) =>
            {
                byte uname_length = bytes[offset];
                offset += 1;

                string uname = System.Text.Encoding.ASCII.GetString(bytes, offset, uname_length);
                offset += uname_length;

                byte[] pword = new byte[length - uname_length - 1];
                System.Buffer.BlockCopy(bytes, offset, pword, 0, pword.Length);

                // Check if this username is in use
                foreach (var c in connected_clients)
                    if (c.username == uname)
                        throw new System.NotImplementedException();

                // Create the player
                var player = representation.create(null, player_prefab_local, 
                    player_prefab_remote, player_spawn);

                // Login
                client.login(uname, pword, player);

                // Load the player as a local on the client
                player.load_on(client, true);
            },

            [global::client.MESSAGE.POSITION_UPDATE] = (client, bytes, offset, length) =>
            {
                int id = System.BitConverter.ToInt32(bytes, offset);
                offset += sizeof(int);

                float x = System.BitConverter.ToSingle(bytes, offset);
                offset += sizeof(float);

                float y = System.BitConverter.ToSingle(bytes, offset);
                offset += sizeof(float);

                float z = System.BitConverter.ToSingle(bytes, offset);
                offset += sizeof(float);

                representations[id].position_update(new Vector3(x, y, z), client);
            }
        };

        message_senders = new Dictionary<MESSAGE, message_sender>
        {
            [MESSAGE.CREATE_LOCAL] = (client, args) =>
                send(client, MESSAGE.CREATE_LOCAL, (byte[])args[0]),

            [MESSAGE.CREATE_REMOTE] = (client, args) =>
                send(client, MESSAGE.CREATE_REMOTE, (byte[])args[0])
        };
    }

    public static void update()
    {
        if (tcp == null) return;

        while (tcp.Pending())
            connected_clients.Add(new client(tcp.AcceptTcpClient()));

        // Recive messages from clients
        foreach (var c in connected_clients)
        {
            byte[] buffer = new byte[c.tcp.ReceiveBufferSize];
            while (c.stream.DataAvailable)
            {
                int bytes_read = c.stream.Read(buffer, 0, buffer.Length);
                int offset = 0;

                while (offset < bytes_read)
                {
                    // Parse message length
                    int payload_length = System.BitConverter.ToInt32(buffer, offset);
                    offset += sizeof(int);

                    // Parse message type
                    var msg_type = (global::client.MESSAGE)buffer[offset];
                    offset += 1;

                    // Handle the message
                    message_parsers[msg_type](c, buffer, offset, payload_length);
                    offset += payload_length;
                }
            }
        }

        // Loop over top-level representations
        foreach (Transform t in transform)
        {
            var rep = t.GetComponent<representation>();
            if (rep == null) continue;

            foreach (var c in connected_clients)
            {
                if (c.player == null)
                    continue; // Client hasn't logged in yet

                float distance = (rep.transform.position - c.player.transform.position).magnitude;
                if (rep.loaded(c))
                {
                    // Unload from clients that are too far away
                    if (distance > c.render_range)
                        rep.unload_on(c);
                }
                else
                {
                    // Load on clients that are within range
                    if (distance < c.render_range)
                        rep.load_on(c, false);
                }
            }
        }
    }

    public enum MESSAGE : byte
    {
        CREATE_LOCAL = 1, // Create a local network object on a client
        CREATE_REMOTE,    // Create a remote network object on a client
        UNLOAD,           // Unload an object on a client
        POSITION_UPDATE,  // Send a position update to a client
    }

    delegate void message_parser(client c, byte[] bytes, int offset, int length);
    static Dictionary<global::client.MESSAGE, message_parser> message_parsers;

    delegate void message_sender(client c, params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;
}

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
    public static string byte_string(byte[] bytes, int offset=0, int length=-1)
    {
        if (length < 0) length = bytes.Length;
        string ret = "";
        for(int i=0; i<length; ++i)
            ret += bytes[offset+i] + ", ";
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
}