using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;

public static class client
{
    static int last_local_id = 0;

    static TcpClient tcp;

    static network_utils.traffic_monitor traffic_up;
    static network_utils.traffic_monitor traffic_down;

    struct pending_creation_message
    {
        public int parent_id;
        public string local_prefab;
        public string remote_prefab;
        public networked creating;
    }
    static Queue<pending_creation_message> pending_creation_messages =
        new Queue<pending_creation_message>();

    public static networked create(Vector3 position,
        string local_prefab, string remote_prefab = null,
        networked parent = null, Quaternion rotation = default, int network_id = -1)
    {
        // If remote prefab not specified, it is the same
        // as the local prefab.
        if (remote_prefab == null)
            remote_prefab = local_prefab;

        // Instantiate the local object, but keep the name
        var created = networked.look_up(local_prefab);
        string name = created.name;
        created = Object.Instantiate(created);
        created.name = name;
        created.local = true; // Created on this client => local

        if (network_id < 0)
        {
            // Assign a (negative) unique local id
            created.network_id = --last_local_id;
        }
        else
        {
            // Copy the given network id
            created.network_id = network_id;
        }
        created.init_network_variables();

        // Parent if requested
        if (parent != null)
            created.transform.SetParent(parent.transform);

        // Create the object with the desired position + rotation
        if (rotation.Equals(default)) rotation = Quaternion.identity;
        created.networked_position = position;
        created.transform.rotation = rotation;

        // Get the id of my parent
        int parent_id = 0;
        if (parent != null) parent_id = parent.network_id;
        if (parent_id < 0) throw new System.Exception("Cannot create children of unregistered objects!");

        // Queue the creation message before on_create is called, so that 
        // we can safely create children in the on_create method (ensure
        // child creation messaage will arrive after my creation message).
        pending_creation_messages.Enqueue(new pending_creation_message
        {
            creating = created,
            parent_id = parent_id,
            local_prefab = local_prefab,
            remote_prefab = remote_prefab,
        });

        // This is being created locally => this is the
        // first time it was created.
        created.on_first_create();
        created.on_create(false);

        if (parent != null) parent.on_add_networked_child(created);
        return created;
    }

    /// <summary> Create an object as instructred by a server message, stored in the given buffer. </summary>
    static void create_from_network(byte[] buffer, int offset, int length, bool local)
    {
        // Record where the end of the serialization is
        int end = offset + length;

        // Deserialize info needed to reproduce the object
        int network_id = network_utils.decode_int(buffer, ref offset);
        int parent_id = network_utils.decode_int(buffer, ref offset);
        string local_prefab = network_utils.decode_string(buffer, ref offset);
        string remote_prefab = network_utils.decode_string(buffer, ref offset);

        // Find the requested parent
        networked parent = parent_id > 0 ? networked.find_by_id(parent_id) : null;

        // Create the reproduction
        var nw = networked.look_up(local ? local_prefab : remote_prefab);
        string name = nw.name;
        nw = Object.Instantiate(nw);
        nw.local = local;
        nw.transform.SetParent(parent?.transform);
        nw.name = name;
        nw.network_id = network_id;
        nw.init_network_variables();

        // Local rotation is intialized to the identity. If rotation
        // is variable, the user should implement that.
        nw.transform.localRotation = Quaternion.identity;

        // The rest is network variables that need deserializing
        int index = 0;
        while (offset < end)
        {
            int nv_length = network_utils.decode_int(buffer, ref offset);
            nw.variable_update(index, buffer, offset, nv_length);
            offset += nv_length;
            index += 1;
        }

        nw.on_create(true);

        if (parent != null) parent.on_add_networked_child(nw);
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

    /// <summary> Send the updated serialization of the variable with the 
    /// given index, belonging to the networked object with the given network id. </summary>
    public static void send_variable_update(int network_id, int index, byte[] serialization)
    {
        message_senders[MESSAGE.VARIABLE_UPDATE](network_id, index, serialization);
    }

    /// <summary> Called when the render range of a player changes. </summary>
    public static void on_render_range_change(networked_player player)
    {
        message_senders[MESSAGE.RENDER_RANGE_UPDATE](player);
    }

    /// <summary> Called when an object is deleted on this client, sends the server that info. </summary>
    public static void on_delete(networked deleted)
    {
        message_senders[MESSAGE.DELETE](deleted.network_id);
    }

    /// <summary> Connect the client to a server. </summary>
    public static void connect(string host, int port, string username, string password)
    {
        // Load networked type info
        networked.load_networked_fields();

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

            [server.MESSAGE.FORCE_CREATE] = (buffer, offset, length) =>
            {
                Vector3 position = network_utils.decode_vector3(buffer, ref offset);
                string local_prefab = network_utils.decode_string(buffer, ref offset);
                string remote_prefab = network_utils.decode_string(buffer, ref offset);
                int network_id = network_utils.decode_int(buffer, ref offset);
                int parent_id = network_utils.decode_int(buffer, ref offset);

                var created = create(position, local_prefab, remote_prefab,
                    parent: parent_id > 0 ? networked.find_by_id(parent_id) : null,
                    network_id: network_id);
            },

            [server.MESSAGE.VARIABLE_UPDATE] = (buffer, offset, length) =>
            {
                // Forward the variable update to the correct object
                int start = offset;
                int id = network_utils.decode_int(buffer, ref offset);
                int index = network_utils.decode_int(buffer, ref offset);
                networked.find_by_id(id).variable_update(index, buffer, offset, length - (offset - start));
            },

            [server.MESSAGE.UNLOAD] = (buffer, offset, length) =>
            {
                // Remove the object from the client
                int id = network_utils.decode_int(buffer, ref offset);
                var found = networked.try_find_by_id(id);
                if (found == null) // This should only happen in high-latency edge cases
                    Debug.Log("Forgetting non-existant id " + id);
                else found.forget();
            },

            [server.MESSAGE.CREATION_SUCCESS] = (buffer, offset, length) =>
            {
                // Update the local id to a network-wide one
                int local_id = network_utils.decode_int(buffer, ref offset);
                int network_id = network_utils.decode_int(buffer, ref offset);
                var nw = networked.find_by_id(local_id);
                nw.network_id = network_id;
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

                int local_id = (int)args[0];
                int parent_id = (int)args[1];
                string local_prefab = (string)args[2];
                string remote_prefab = (string)args[3];
                byte[] variable_serializations = (byte[])args[4];

                send(MESSAGE.CREATE, network_utils.concat_buffers(
                    network_utils.encode_int(local_id),
                    network_utils.encode_int(parent_id),
                    network_utils.encode_string(local_prefab),
                    network_utils.encode_string(remote_prefab),
                    variable_serializations
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

            [MESSAGE.RENDER_RANGE_UPDATE] = (args) =>
            {
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                var nw = (networked_player)args[0];

                // Send the new render range
                send(MESSAGE.RENDER_RANGE_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_float(nw.render_range)
                ));
            },

            [MESSAGE.VARIABLE_UPDATE] = (args) =>
            {
                if (args.Length != 3)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int network_id = (int)args[0];
                int index = (int)args[1];
                byte[] serialization = (byte[])args[2];

                send(MESSAGE.VARIABLE_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_int(network_id),
                    network_utils.encode_int(index),
                    serialization
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

        // Queue pending creation messages (delayed until now
        // so that for each created object we have the most
        // up-to-date network variables).
        while (pending_creation_messages.Count > 0)
        {
            var cm = pending_creation_messages.Dequeue();

            if (cm.creating == null)
                continue; // This object has since been deleted

            // Request creation on the server
            message_senders[MESSAGE.CREATE](cm.creating.network_id,
                cm.parent_id, cm.local_prefab, cm.remote_prefab,
                cm.creating.serialize_networked_variables());
        }

        // Run network_update for each object
        networked.network_updates();

        // Send messages
        send_queued_messages();
    }

    public static string info()
    {
        if (tcp == null) return "Client not connected.";
        return "Client connected\n" +
               networked.objects_info() + "\n" +
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
        RENDER_RANGE_UPDATE, // Client render range has changed
        VARIABLE_UPDATE,     // A networked_variable has changed
    }

    delegate void message_sender(params object[] args);
    static Dictionary<MESSAGE, message_sender> message_senders;

    delegate void message_parser(byte[] message, int offset, int length);
    static Dictionary<server.MESSAGE, message_parser> message_parsers;
}