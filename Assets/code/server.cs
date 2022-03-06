using System.Collections;
using System.Collections.Generic;

#if STANDALONE_SERVER
#else
using UnityEngine;
#endif

public static class server
{
    //###########//
    // CONSTANTS //
    //###########//

    /// <summary> Clients that have been silent for longer than this are disconnected </summary>
    public const float CLIENT_TIMEOUT = 6f;

    /// <summary> Autosave every this many seconds. </summary>
    public const float AUTOSAVE_PERIOD = 5 * 60f;

    /// <summary> Clients that have been inactive for longer than this are disconnected </summary>
    public const float CLIENT_ACTIVITY_TIMEOUT = 60f;

    /// <summary> How often a client should send a heartbeat 
    /// (both to avoid timeout, and to measure ping). </summary>
    public const float CLIENT_HEARTBEAT_PERIOD = 1f;

    /// <summary> The position resolution to which players are updated about each other, even
    /// when they are out of render range on each others clients. </summary>
    public const float PLAYER_RESOLUTION_UNLOADED = 5f;

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
        // The username + id of this client
        public string username { get; private set; }
        public ulong user_id { get; private set; }

        /// <summary> The representation of this clients player object. </summary>
        public representation player
        {
            get => _player;
            set
            {
                if (_player == value) return; // No change
                if (_player != null) throw new System.Exception("Client already has a player!");

                _player = value;
                player_representations[user_id] = value;

                // Update other clients about this new player
                foreach (var c in connected_clients)
                    if (c != this)
                        send_message(MESSAGE.PLAYER_UPDATE, c, username,
                            player.local_position, connected_clients.Contains(this));
            }
        }
        representation _player;

        /// <summary> The last position of the player that other clients were told about. </summary>
        Vector3 last_updated_position;

        // The connection to this client
        public client_backend backend { get; private set; }
        public backend_stream stream => backend.stream;

        public float render_range = INIT_RENDER_RANGE;

        // The last time we reccived a message from this client
        public float last_message_time = 0;

        // The last time we reccived an active hearbeat from this client
        public float last_active_time = 0;

        public client(client_backend client_backend)
        {
            backend = client_backend;
            last_active_time = Time.realtimeSinceStartup;
        }

        public void login(string username, ulong user_id)
        {
            // Attempt to load the player
            representation player = null;
            if (!player_representations.TryGetValue(user_id, out player))
            {
                // Force the creation of the player on the client
                player = null;
                send_message(MESSAGE.FORCE_CREATE, this,
                    Vector3.zero, player_prefab,
                    ++representation.last_network_id_assigned, 0
                );
            }

            this.username = username;
            this.user_id = user_id;

            if (player != null)
            {
                this.player = player;
                player.parent = active_representations;
                load(player);
            }

            // Update this client about other already-loaded players
            foreach (var c in connected_clients)
                if (c != this && c.player != null)
                    send_message(MESSAGE.PLAYER_UPDATE, this, c.username, c.player.local_position, true);
        }

        /// <summary> Called when a client disconnects. If message is not 
        /// null, it is sent to the server as part of a DISCONNECT message, 
        /// otherwise no DISCONNECT message is sent to the server. </summary>
        public void disconnect(string message, bool delete_player = false, bool send_dc_message = true)
        {
            // Replace empty, or null message with "No information" message.
            if (message == null || message.Trim().Length == 0)
                message = "No information";

            // Unload representations (only top-level, lower level
            // will be automatically unloaded by the unload function)
            foreach (var rep in new HashSet<representation>(loaded))
                if (rep.is_top_level)
                    unload(rep, false);

            // Log/send a disconnect message
            log(username + " disconnected: " + message);
            if (send_dc_message)
                send_message(MESSAGE.DISCONNECT, this, message);

            // Send any queued messages for this
            // client before we get rid of the queue
            send_queued_messages(this);
            message_queues.Remove(this);

            // Remove the client from connected clients collection
            connected_clients.Remove(this);

            // Call backend-specific disconnect and close with
            // a timeout, so that any hanging messages
            // (in particular the DISCONNECT message) can be sent.
            server.backend.on_disconnect(backend);
            backend.Close((int)(server.CLIENT_TIMEOUT * 1000));

            // Disconnect the player if there is one loaded
            // (the player isn't loaded if, for example, this
            // client attempted to login with an in-use username)
            if (player != null) disconnect_player(delete_player);
        }

        /// <summary> Called when the player on this client is 
        ///  unloaded as the result of a disconnect. </summary>
        void disconnect_player(bool delete_player)
        {
            // Remember the position that the player disonnected at
            Vector3 disconnect_position = player.local_position;

            if (delete_player)
                // Delete the player
                player.delete();
            else
                // Just unload the player (also remove it from representations
                // so that it doens't just get re-loaded based on proximity)
                foreach (var c in connected_clients)
                    if (c.has_loaded(player))
                        c.unload(player, false);

            if (player.parent == deleted_representations)
                // Player has been deleted, remove it from player representations
                player_representations.Remove(user_id);
            else
                // Player has not been deleted (just logged out), move to inactive
                player.parent = inactive_representations;

            // Let other clients know that we've disconnected
            foreach (var c in connected_clients)
                send_message(MESSAGE.PLAYER_UPDATE, c, username, disconnect_position, false);
        }

        /// <summary> The representations loaded as objects on this client. </summary>
        HashSet<representation> loaded = new HashSet<representation>();

        /// <summary> Returns true if the client should load the provided representation. </summary>
        bool should_load(representation rep)
        {
            return (rep.local_position - player.local_position).magnitude <
                rep.radius + render_range;
        }

        /// <summary> Called once per network update, just after messages 
        /// are recived and just before messages are sent. </summary>
        public void update()
        {
            if (player == null)
                return; // Only update loaded if we have a player

            // Figure out which representations need loading or unloading this frame
            List<representation> to_unload = new List<representation>();
            List<representation> to_load = new List<representation>();

            // Loop over active representations
            foreach (var elm in active_representations)
            {
                if (elm is representation)
                {
                    var rep = (representation)elm;
                    if (has_loaded(rep))
                    {
                        // Unload from clients that are too far away
                        if (!should_load(rep))
                            to_unload.Add(rep);
                    }
                    else
                    {
                        // Load on clients that are within range
                        if (should_load(rep))
                            to_load.Add(rep);
                    }
                }
            }

            // Load or unload representations that need loading/unloading
            // (this is done as a seperate step to avoid modifying active_representations
            //  during enumeration / avoid having to make a copy of active_representations
            //  which is quite large)
            foreach (var r in to_unload) unload(r, false);
            foreach (var r in to_load) load(r, false);

            // Let other clients know about our updated position, if we've moved far enough
            if ((last_updated_position - player.local_position).magnitude > PLAYER_RESOLUTION_UNLOADED)
            {
                last_updated_position = player.local_position;
                foreach (var c in connected_clients)
                {
                    if (c == this) continue;
                    send_message(MESSAGE.PLAYER_UPDATE, c, username,
                        player.local_position, connected_clients.Contains(this));
                }
            }

#if (UNITY_EDITOR || NO_SERVER_TIMEOUT)
            // Don't time out clients if the server is the editor, so that
            // we don't time people out if the editor is paused.
#else
            // Check if we've timed out, if so disconnect, but with
            // a large timeout to send remaining messages, in the
            // off chance that the client will actually recive them.

            // Time out clients that have been inactive for a while
            float time_since_last_active = Time.realtimeSinceStartup - last_active_time;
            if (time_since_last_active > CLIENT_ACTIVITY_TIMEOUT)
                disconnect("Disconnected due to inactivity", timeout: 10);

            // Time out client from which we have not reccived a message in a while
            float time_since_last_message = Time.realtimeSinceStartup - last_message_time;
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
            rep.recurse_top_down((elm) =>
            {
                if (elm is representation)
                {
                    var loading = (representation)elm;
                    if (already_created && loading != rep)
                        throw new System.Exception("A representation with children should not be already_created!");

                    if (!already_created)
                        send_message(MESSAGE.CREATE, this, loading.serialize());

                    // Add this object to the loaded set
                    loaded.Add(loading);
                    loading.on_load_on(this);
                }
            });
        }

        /// <summary>  Unload the object corresponding to the given 
        /// representation on this client. </summary>
        public void unload(representation rep, bool deleting, bool already_removed = false)
        {
            // Unload rep and all of it's children
            rep.recurse_top_down((elm) =>
            {
                if (elm is representation)
                {
                    var unloading = (representation)elm;

                    if (!loaded.Contains(unloading))
                    {
                        string err = "Client " + username + " tried to unload an object (" +
                                     "id = " + unloading.network_id + ") which was not loaded!";
                        throw new System.Exception(err);
                    }

                    // Remove this object from the loaded set
                    loaded.Remove(unloading);
                    unloading.on_unload_on(this);
                }
            });

            // Let the client know that rep has been unloaded
            // (the client will automatically unload it's children also)
            if (!already_removed)
                send_message(MESSAGE.UNLOAD, this, rep.network_id, deleting);
        }
    }

    //####################//
    // HIERRARCHY ELEMENT //
    //####################//

    /// <summary> Allows the storage of objects in a parent-child type hierarchy. </summary>
    class hierarchy_element : IEnumerable<hierarchy_element>
    {
        public hierarchy_element(string display_name)
        {
            this.display_name = display_name;
        }
        public string display_name { get; private set; }

        public hierarchy_element parent
        {
            get => _parent;
            set
            {
                if (_parent == value) return;
                _parent?.children.Remove(this);
                _parent = value;
                _parent?.children.Add(this);
            }
        }
        hierarchy_element _parent;

        HashSet<hierarchy_element> children = new HashSet<hierarchy_element>();
        public IEnumerator<hierarchy_element> GetEnumerator() { return children.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary> Call the function <paramref name="f"/> on me
        /// and my children recursively downward. </summary>
        public void recurse_top_down(recurse_func f)
        {
            var to_apply = new Queue<hierarchy_element>();
            to_apply.Enqueue(this);

            while (to_apply.Count > 0)
            {
                var applying_to = to_apply.Dequeue();
                foreach (var c in applying_to) to_apply.Enqueue(c);
                f(applying_to);
            }
        }
        public delegate void recurse_func(hierarchy_element r);
    }


    //################//
    // REPRESENTATION //
    //################//


    /// <summary> Represents a networked object on the server. </summary>
    class representation : hierarchy_element
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
                    send_message(MESSAGE.LOSE_AUTH, _authority, network_id);

                _authority = value;

                // New client gains authority
                if (_authority != null)
                    send_message(MESSAGE.GAIN_AUTH, _authority, network_id);
            }
        }
        client _authority;

        /// <summary> The local position of this representation (needed for proximity tests). </summary>
        public Vector3 local_position;

        /// <summary> Returns true if this is a top-level representation. </summary>
        public bool is_top_level =>
            parent == active_representations ||
            parent == inactive_representations;

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
            parent = deleted_representations;

            // Remove/destroy the representation + all children
            recurse_top_down((elm) =>
            {
                if (elm is representation)
                {
                    // Move the id to the recently deleted collection
                    var r = (representation)elm;
                    representations.Remove(r.network_id);
                    recently_deleted[r.network_id] = Time.realtimeSinceStartup;
                }
            });

            // Delete successful. If the client requested a response, send one.
            if (response_requested)
                send_message(MESSAGE.DELETE_SUCCESS, issued_from, network_id);
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
            // Don't mess with player authority
            if (this == client.player)
                return;

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
            int offset = 0;
            if (i == (int)engine_networked_variable.TYPE.POSITION_X)
                local_position.x = network_utils.decode_float(serial, ref offset);
            else if (i == (int)engine_networked_variable.TYPE.POSITION_Y)
                local_position.y = network_utils.decode_float(serial, ref offset);
            else if (i == (int)engine_networked_variable.TYPE.POSITION_Z)
                local_position.z = network_utils.decode_float(serial, ref offset);

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
                    send_message(MESSAGE.VARIABLE_UPDATE, c, network_id, index, new_serialization);
        }

        /// <summary> Trigger a network event on all clients that have this representation 
        /// loaded (apart from the client that triggered the event). </summary>
        public void trigger_network_event(client triggered_by, int event_number)
        {
            foreach (var c in connected_clients)
                if ((c != triggered_by) && c.has_loaded(this))
                    send_message(MESSAGE.TRIGGER, c, network_id, event_number);
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
            // Parent_id = 0 if I am not a child of another representation
            int parent_id = 0;
            if (parent != null && parent is representation)
                parent_id = ((representation)parent).network_id;

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

        /// <summary> Representations can only be made using the 
        /// <see cref="create(byte[], int, int, out int)"/> method. </summary>
        private representation(string display_name) : base(display_name) { }

        /// <summary> Return some nicely formatted information 
        /// about this representation. </summary>
        public string info()
        {
            string ret = "";
            ret += string.Format("Network id     : {0}\n", network_id);
            ret += string.Format("Authority      : {0}\n", authority.username);
            ret += string.Format("Parent id      : {0}\n", parent.display_name);
            ret += string.Format("Local position : {0}\n", local_position);
            ret += string.Format("Persistant     : {0}\n", persistant);
            ret += string.Format("Prefab         : {0}\n", prefab);
            ret += string.Format("Radius         : {0}\n", radius);
            ret += "Serializations:\n";
            for (int i = 0; i < serializations.Count; ++i)
                ret += "  [" + i + "] " + network_utils.serial_info(serializations[i]) + "\n";
            return ret;
        }

        //##############//
        // STATIC STUFF //
        //##############//

        public static int last_network_id_assigned = 0;

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

            // Could not identify the requested prefab
            if (networked.look_up(prefab) == null)
            {
                log_warning("Could not identify the prefab: " + prefab);
                return null;
            }

            // Could not find the requested parent
            if (parent_id > 0 && !representations.ContainsKey(parent_id))
            {
                log_warning("Tried to create a child of the missing id: " + parent_id);
                return null;
            }

            // Figure out the network id to create
            int network_id = input_id;

            // This was a local id, assign a new, unique id
            if (network_id < 0)
                network_id = ++last_network_id_assigned;

            // Ensure last_network_id_assigned remains >= all assigned network ids
            else if (network_id > last_network_id_assigned)
                last_network_id_assigned = network_id;

            // Create the representation
            representation rep = new representation(network_id.ToString());
            if (parent_id > 0) rep.parent = representations[parent_id];
            else rep.parent = active_representations;
            rep.prefab = prefab;
            rep.network_id = network_id;

            // Everything else in serialisation is networked variables to deserialize
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
    }


    //##############//
    // SERVER LOGIC //
    //##############//

    // STATE VARIABLES //

    /// <summary> The backend that the server is listening with. </summary>
    static server_backend backend;

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

    /// <summary> Player representations on the server, keyed by user id. </summary>
    static Dictionary<ulong, representation> player_representations;

    /// <summary> Transform containing active representations (those which are
    /// considered for existance on clients) </summary>
    static hierarchy_element active_representations;

    /// <summary> Representations that are not considered for existance
    /// on clients, but need to be remembered
    /// (such as logged out players) </summary>
    static hierarchy_element inactive_representations;

    /// <summary> Representations that are in the process of being deleted. </summary>
    static hierarchy_element deleted_representations;

    /// <summary> Messages that are yet to be sent. </summary>
    static Dictionary<client, Queue<pending_message>> message_queues;

    /// <summary> Bytes of truncated messages from clients that appeared 
    /// at the end of a read buffer, ready to be glued to the start of the 
    /// next buffer. </summary>
    static Dictionary<client, byte[]> truncated_read_messages;

    // Traffic monitors
    static network_utils.traffic_monitor traffic_down;
    static network_utils.traffic_monitor traffic_up;

    /// <summary> The time the server was last autosaved. </summary>
    static float last_autosave_time;

    // END STATE VARIABLES //


    // DERIVED STATE //

    /// <summary> Returns true if the server has been started. </summary>
    public static bool started { get => backend != null; }

    // END DERIVED STATE //


    /// <summary> Start a server listening on the given port on the local machine. </summary>
    public static bool start(string savename, string player_prefab, out string error_message)
    {
        if (started)
        {
            error_message = "Server already started!";
            return false;
        }

        // Initialize state variables
        server.player_prefab = player_prefab;
        server.savename = savename;
        backend = server_backend.default_backend();
        traffic_up = new network_utils.traffic_monitor();
        traffic_down = new network_utils.traffic_monitor();
        connected_clients = new HashSet<client>();
        representations = new Dictionary<int, representation>();
        recently_deleted = new Dictionary<int, float>();
        player_representations = new Dictionary<ulong, representation>();
        message_queues = new Dictionary<client, Queue<pending_message>>();
        active_representations = new hierarchy_element("Active representations");
        inactive_representations = new hierarchy_element("Inactive representations");
        deleted_representations = new hierarchy_element("Deleted representations");
        truncated_read_messages = new Dictionary<client, byte[]>();
        last_autosave_time = Time.realtimeSinceStartup;

        // Start listening
        try
        {
            backend.Start();
        }
        catch (System.Exception e)
        {
            error_message = e.Message;
            return false;
        }

        // Load the world (or the startup world)
        if (System.IO.File.Exists(save_file()))
            load();
        else
            load(startup_world: true);

#if STANDALONE_SERVER
        // Error out if the save file does not exist
        else
        {
            log_error("Save file does not exist: " + save_file());
            log_error("The standalone server requires an existing savefile.");
            error_message = "Save file not present.";
            return false;
        }
#       else
        // Check that server configuration is valid
        if (!networked.look_up(player_prefab).GetType().IsSubclassOf(typeof(networked_player)))
            throw new System.Exception("Local player object must be a networked_player!");
#       endif

        // Server started successfully
        error_message = "";
        return true;
    }

    public static void stop()
    {
        if (!started) return;

        foreach (var c in new List<client>(connected_clients))
            c.disconnect("Server stopped.");

        backend.Stop();
        save();
        backend = null;
    }

    public static void update()
    {
        if (!started) return;

        backend.Update();

        // Timout recently-deleted id's
        HashSet<int> to_remove = new HashSet<int>();
        foreach (var kv in recently_deleted)
            if (Time.realtimeSinceStartup - kv.Value > CLIENT_TIMEOUT)
                to_remove.Add(kv.Key);

        foreach (var i in to_remove)
            recently_deleted.Remove(i);

        // Connect new clients
        while (backend.Pending())
            connected_clients.Add(new client(backend.AcceptClient()));

        // Disconnect clients that have dropped
        foreach (var c in new List<client>(connected_clients))
            if (c.backend.has_disconnected)
                c.disconnect("Backend disconnected");

        // Recive messages from clients
        foreach (var c in new List<client>(connected_clients))
        {
            // Message handler for this client
            network_utils.message_handler hander = (type_byte, buffer, offset, payload_length) =>
            {
                receive_message(c, type_byte, buffer, offset, payload_length);
            };

            byte[] buffer = new byte[c.backend.ReceiveBufferSize];
            while (c.stream.CanRead && c.stream.DataAvailable)
            {
                int buffer_start = 0;

                if (truncated_read_messages.TryGetValue(c, out byte[] trunc))
                {
                    // Glue a truncated message onto the start of the buffer
                    if (trunc.Length > buffer.Length)
                        throw new System.Exception("Truncated message too large for buffer!");
                    System.Buffer.BlockCopy(trunc, 0, buffer, 0, trunc.Length);
                    buffer_start = trunc.Length;
                    truncated_read_messages.Remove(c);
                }

                // Read new bytes into the buffer
                int bytes_read = c.stream.Read(buffer, buffer_start,
                    buffer.Length - buffer_start);
                traffic_down.log_bytes(bytes_read);

                // Work out how much data is in the buffer (including data
                // potentially copied from a previous truncation) and
                // initialze reading at the beginning.
                int data_bytes = bytes_read + buffer_start;
                int offset = 0;

                while (offset < data_bytes)
                {
                    if (!network_utils.decompose_message(
                        buffer, ref offset, data_bytes,
                        out int last_message_start, hander))
                    {
                        // Save the truncated message for later
                        byte[] to_save = new byte[data_bytes - last_message_start];
                        System.Buffer.BlockCopy(buffer, last_message_start, to_save, 0, to_save.Length);
                        truncated_read_messages[c] = to_save;
                        break;
                    }
                }
            }
        }

        // Update the objects which are loaded on the clients
        foreach (var c in new List<client>(connected_clients))
            c.update();

        // Send any pending messages
        send_queued_messages();

        // Autosave
        if (Time.realtimeSinceStartup > last_autosave_time + AUTOSAVE_PERIOD)
        {
            last_autosave_time = Time.realtimeSinceStartup;
            save(autosave: true);
            log("Server has autosaved.");
        }
    }

    /// <summary> Send all the messages from the queue 
    /// (or just the ones for the given client, if specified). </summary>
    static void send_queued_messages(client for_client = null)
    {
        // Send the messages from the queue
        var disconnected_during_write = new List<client>();
        foreach (var kv in message_queues)
        {
            var client = kv.Key;
            var queue = kv.Value;

            if (for_client != null && client != for_client)
                continue; // We asked for a specific client, this isn't them

            // The buffer to concatinate messages into
            byte[] send_buffer = new byte[client.backend.SendBufferSize];
            int offset = 0;
            bool disconnected = false;

            while (queue.Count > 0)
            {
                var msg = queue.Dequeue();

                if (msg.bytes.Length > send_buffer.Length)
                    throw new System.Exception("Message too large!");

                if (offset + msg.bytes.Length > send_buffer.Length)
                {
                    // Message would overrun buffer, send
                    // the buffer and create a new one
                    traffic_up.log_bytes(offset);
                    try { client.stream.Write(send_buffer, 0, offset); }
                    catch { disconnected = true; break; }
                    send_buffer = new byte[client.backend.SendBufferSize];
                    offset = 0;
                }

                // Copy the message into the send buffer
                System.Buffer.BlockCopy(msg.bytes, 0, send_buffer, offset, msg.bytes.Length);
                offset += msg.bytes.Length; // Move to next message
            }

            // Send the buffer
            if (!disconnected && offset > 0)
            {
                traffic_up.log_bytes(offset);
                try { client.stream.Write(send_buffer, 0, offset); }
                catch { disconnected = true; }
            }

            // Record clients that got disconnected during a stream.Write attempt
            if (disconnected)
                disconnected_during_write.Add(client);
        }

        // Properly disconnect clients that were found
        // to have disconnected during message writing
        foreach (var d in disconnected_during_write)
            d.disconnect(null);
    }

    //################//
    // SAVING/LOADING //
    //################//

    static void load(bool startup_world = false)
    {
        string fullpath = null;

        if (startup_world)
            fullpath = Application.streamingAssetsPath + "/startup.save";
        else
            fullpath = System.IO.Path.GetFullPath(save_file());

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
                if (rep == null) continue; // Representation creation failed - likely prefab no longer exists

                // Check the network id recovered makes sense
                if (input_id < 0) throw new System.Exception("Loaded unregistered representation!");
                if (input_id != rep.network_id) throw new System.Exception("Network id loaded incorrectly!");

                switch (type)
                {
                    case SAVE_TYPE.PLAYER:

                        // For players, deserialize also the username
                        buffer.Read(length_bytes, 0, sizeof(int));
                        length = System.BitConverter.ToInt32(length_bytes, 0);
                        byte[] id_bytes = new byte[length];
                        buffer.Read(id_bytes, 0, length);
                        int id_offset = 0;
                        var id = network_utils.decode_ulong(id_bytes, ref id_offset);

                        // Players start inactive
                        rep.parent = inactive_representations;
                        player_representations[id] = rep;
                        break;

                    case SAVE_TYPE.ACTIVE:

                        // Nothing needs doing
                        break;

                    case SAVE_TYPE.INACTIVE:

                        // If this is a top-level representation, move to inactive
                        if (rep.is_top_level)
                            rep.parent = inactive_representations;
                        break;

                    default:
                        throw new System.Exception("Unkown save type: " + type);
                }
            }
        }
    }

    public static void save(bool autosave = false, bool is_startup = false)
    {
        // The file containing the savegame
        string filename = autosave ? autosave_file() : save_file();

        if (is_startup)
        {
            if (!Application.isEditor)
            {
                Debug.LogError("Tried to save startup file from outside of the editor!");
                return;
            }

            Debug.Log(Application.streamingAssetsPath);
            filename = Application.streamingAssetsPath + "/startup.save";
        }

        bool should_save(int id)
        {
            // Don't save things tagged as INotSavedInStartupFile
            if (is_startup && networked.try_find_by_id(id, error_if_not_recently_forgotten: false)?.
                GetComponentInParent<INotSavedInStartupFile>() != null)
                return false;

            return true;
        }

        using (var file = System.IO.File.OpenWrite(filename))
        using (var compressor = new System.IO.Compression.GZipStream(file,
            System.IO.Compression.CompressionLevel.Optimal))
        {
            // Remember which network_id's have been saved
            HashSet<int> saved = new HashSet<int>();

            // Save the players first
            foreach (var kv in player_representations)
            {
                saved.Add(kv.Value.network_id);

                if (!should_save(kv.Value.network_id))
                    continue;

                compressor.WriteByte((byte)SAVE_TYPE.PLAYER);
                compressor.write_bytes_with_length(kv.Value.serialize());

                // Write also the id
                compressor.write_bytes_with_length(network_utils.encode_ulong(kv.Key));
            }

            // Then save active representations
            active_representations.recurse_top_down((elm) =>
            {
                if (elm is representation)
                {
                    var rep = (representation)elm;
                    if (saved.Contains(rep.network_id) || !rep.persistant) return;
                    saved.Add(rep.network_id);

                    if (!should_save(rep.network_id))
                        return;

                    compressor.WriteByte((byte)SAVE_TYPE.ACTIVE);
                    compressor.write_bytes_with_length(rep.serialize());
                }
            });

            // Then save inactive representations
            inactive_representations.recurse_top_down((elm) =>
            {
                if (elm is representation)
                {
                    var rep = (representation)elm;
                    if (saved.Contains(rep.network_id) || !rep.persistant) return;
                    saved.Add(rep.network_id);

                    if (!should_save(rep.network_id))
                        return;

                    compressor.WriteByte((byte)SAVE_TYPE.INACTIVE);
                    compressor.write_bytes_with_length(rep.serialize());
                }
            });
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

    /// <summary> The directory in which games are saved. </summary>
    public static string saves_dir()
    {
        // Ensure the saves/ directory exists
        string saves_dir = Application.persistentDataPath + "/saves";
        if (!System.IO.Directory.Exists(saves_dir))
            System.IO.Directory.CreateDirectory(saves_dir);
        return saves_dir;
    }

    /// <summary> The file that this session is saved in. </summary>
    public static string save_file()
    {
        return saves_dir() + "/" + savename + ".save";
    }

    /// <summary> The file that this session is autosaved to. </summary>
    public static string autosave_file()
    {
        return saves_dir() + "/" + savename + "_autosave.save";
    }

    /// <summary> Get an array of all the save files on this machine. </summary>
    public static string[] existing_saves(bool include_autosaves = false)
    {
        List<string> saves = new List<string>();
        foreach (var f in System.IO.Directory.GetFiles(saves_dir()))
        {
            if (!f.EndsWith(".save")) continue;
            if (!include_autosaves && f.Contains("autosave")) continue;
            saves.Add(f);
        }
        return saves.ToArray();
    }

    /// <summary> Returns true if the save with the given name already exists. </summary>
    public static bool save_exists(string savename)
    {
        return System.IO.File.Exists(saves_dir() + "/" + savename + ".save");
    }

    //###########//
    // UTILITIES //
    //###########//

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
            else log_warning(msg);
            return null;
        }

        return rep;
    }
    public static int delete_all_representations_with_prefab(string prefab)
    {
        int ret = 0;
        foreach (var kv in new Dictionary<int, representation>(representations))
            if (kv.Value.prefab == prefab)
            {
                kv.Value.delete();
                ++ret;
            }
        return ret;
    }

    /// <summary> A server message waiting to be sent. </summary>
    struct pending_message
    {
        public byte[] bytes;
        public float send_time;
    }

    /// <summary> Returns general information about the server. </summary>
    public static string info()
    {
        if (!started) return "Server not started.";
        return "Server listening on " + backend.local_address + "\n" +
               "    Server version     : " + version + "\n" +
               "    Connected clients  : " + connected_clients.Count + "\n" +
               "    Representations    : " + representations.Count + "\n" +
               "    Recently deleted   : " + recently_deleted.Count + "\n" +
               "    Upload             : " + traffic_up.usage() + "\n" +
               "    Download           : " + traffic_down.usage();
    }

    /// <summary> Returns infromation stored on the server
    /// about a particular network id. </summary>
    public static string info(int network_id)
    {
        if (!started) return "Server must be started to use this method.";
        if (!representations.TryGetValue(network_id, out representation rep))
            return "No representation with this id found on the server";
        return rep.info();
    }

    //###########//
    // MESSAGING //
    //###########//

    public enum MESSAGE : byte
    {
        // Numbering starts above client message types, so they are not confused
        CREATE = global::client.LARGEST_VALUE_MESSAGE + 1,
        // Create a networked object on a client
        FORCE_CREATE,      // Force a client to create an object
        UNLOAD,            // Unload an object on a client
        CREATION_SUCCESS,  // Send when a creation requested by a client was successful
        DELETE_SUCCESS,    // Send when a client deletes a networked object and requests a response
        VARIABLE_UPDATE,   // Send a networked_variable update to a client
        TRIGGER,           // Send an event trigger from an object to all instances of same object
        LOSE_AUTH,         // Sent to a client when they lose authority over an object
        GAIN_AUTH,         // Sent to a ciient when they gain authority over an object
        HEARTBEAT,         // Respond to a client heartbeat
        DISCONNECT,        // Sent to a client when they are disconnected
        PLAYER_UPDATE,     // Sent to clients to update info about connected players
        JOINABLE_RESPONSE, // Response to queries if the server is joinable
    }

    // Send a payload to a client
    static void send(client client, MESSAGE msg_type, byte[] payload, bool immediate = false)
    {
        byte[] to_send = network_utils.compose_message((byte)msg_type, payload);

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
                client.disconnect(null, send_dc_message: false);
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

    /// <summary> Send a message with the given type to the 
    /// given client with the given arguments </summary>
    static void send_message(MESSAGE type, client client, params object[] args)
    {
        switch (type)
        {
            case MESSAGE.CREATE:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                send(client, MESSAGE.CREATE, (byte[])args[0]);
                break;

            case MESSAGE.FORCE_CREATE:
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
                break;

            case MESSAGE.UNLOAD:
                if (args.Length != 2)
                    throw new System.ArgumentException("Wrong number of arguments!");

                network_id = (int)args[0];
                bool deleting = (bool)args[1];

                send(client, MESSAGE.UNLOAD, network_utils.concat_buffers(
                    network_utils.encode_int(network_id),
                    network_utils.encode_bool(deleting)
                ));
                break;

            case MESSAGE.VARIABLE_UPDATE:

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
                break;

            case MESSAGE.TRIGGER:
                if (args.Length != 2)
                    throw new System.Exception("Wrong number of arguments!");

                id = (int)args[0];
                var number = (int)args[1];

                send(client, MESSAGE.TRIGGER, network_utils.concat_buffers(
                    network_utils.encode_int(id),
                    network_utils.encode_int(number)
                ));
                break;


            case MESSAGE.CREATION_SUCCESS:
                if (args.Length != 2)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int local_id = (int)args[0];
                network_id = (int)args[1];

                send(client, MESSAGE.CREATION_SUCCESS, network_utils.concat_buffers(
                    network_utils.encode_int(local_id),
                    network_utils.encode_int(network_id)
                ));
                break;

            case MESSAGE.DELETE_SUCCESS:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                network_id = (int)args[0];
                send(client, MESSAGE.DELETE_SUCCESS, network_utils.encode_int(network_id));
                break;

            case MESSAGE.GAIN_AUTH:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                network_id = (int)args[0];
                if (network_id <= 0)
                    throw new System.Exception("Can't gain authority over unregistered object!");

                send(client, MESSAGE.GAIN_AUTH, network_utils.encode_int(network_id));
                break;

            case MESSAGE.LOSE_AUTH:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                network_id = (int)args[0];
                if (network_id <= 0)
                    throw new System.Exception("Can't lose authority over unregistered object!");

                send(client, MESSAGE.GAIN_AUTH, network_utils.encode_int(network_id));
                break;

            case MESSAGE.HEARTBEAT:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                int heartbeat_key = (int)args[0];
                var dt = System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
                int seconds_since_epoch = (int)dt.TotalSeconds; // See you in 2038!

                send(client, MESSAGE.HEARTBEAT, network_utils.concat_buffers(
                        network_utils.encode_int(heartbeat_key),
                        network_utils.encode_int(seconds_since_epoch)
                    ));
                break;

            case MESSAGE.DISCONNECT:

                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                // The disconnection message
                string msg = (string)args[0];
                if (msg == null)
                    throw new System.Exception("Disconnect messages should not be sent without a payload!");

                // Disconnect messages are sent immediately, so that the client object (including 
                // it's message queues) can be immediately removed afterwards
                send(client, MESSAGE.DISCONNECT, network_utils.encode_string(msg), immediate: true);
                break;

            case MESSAGE.PLAYER_UPDATE:
                if (args.Length != 3)
                    throw new System.ArgumentException("Wrong number of arguments!");

                // The username of the player we're sending updates about
                string username = (string)args[0];
                position = (Vector3)args[1];
                bool connected = (bool)args[2];

                send(client, MESSAGE.PLAYER_UPDATE, network_utils.concat_buffers(
                    network_utils.encode_string(username),
                    network_utils.encode_vector3(position),
                    network_utils.encode_bool(connected)
                ));
                break;

            case MESSAGE.JOINABLE_RESPONSE:
                if (args.Length != 1)
                    throw new System.ArgumentException("Wrong number of arguments!");

                // Is the server joinable?
                bool response = (bool)args[0];

                send(client, MESSAGE.JOINABLE_RESPONSE, network_utils.encode_bool(response));
                break;

            default:
                throw new System.Exception("Unkown message type!");
        };
    }

    /// <summary> Receive a message of the given <paramref name="type"/> stored between <paramref name="offset"/> and 
    /// <paramref name="offset"/>+<paramref name="length"/> in <paramref name="bytes"/>. </summary>
    static void receive_message(client client, byte type_byte, byte[] bytes, int offset, int length)
    {
        if (!System.Enum.IsDefined(typeof(global::client.MESSAGE), type_byte))
            throw new System.Exception(type_byte + " is not a valid message-to-server type!");

        var type = (global::client.MESSAGE)type_byte;

        switch (type)
        {
            case global::client.MESSAGE.LOGIN:

                // Get username and id from message bytes
                string uname = network_utils.decode_string(bytes, ref offset);
                ulong user_id = network_utils.decode_ulong(bytes, ref offset);
                string client_ver = network_utils.decode_string(bytes, ref offset);

                // Check for version mismatch
                if (client_ver != version)
                {
                    client.disconnect("Server version (" + version + ") and client version (" + client_ver + ") differ!");
                    return;
                }

                // Check if this id is in use
                foreach (var c in connected_clients)
                    if (c.user_id == user_id)
                    {
                        client.disconnect("User id already in use.");
                        return;
                    }

                // Login
                client.login(uname, user_id);
                log(uname + " connected.");
                break;

            case global::client.MESSAGE.DISCONNECT:
                // No need to send a server.DISCONNECT message to
                // the client as they requested the disconnect
                bool delete_player = network_utils.decode_bool(bytes, ref offset);
                client.disconnect(null, delete_player: delete_player);
                break;

            case global::client.MESSAGE.HEARTBEAT:

                // This client is still kicking - respond so they can time the ping
                bool activity = network_utils.decode_bool(bytes, ref offset);
                int heartbeat_key = network_utils.decode_int(bytes, ref offset);
                if (activity) client.last_active_time = Time.realtimeSinceStartup;
                send_message(MESSAGE.HEARTBEAT, client, heartbeat_key);
                break;

            case global::client.MESSAGE.VARIABLE_UPDATE:

                // Forward the updated variable serialization to the correct representation
                int start = offset;
                int id = network_utils.decode_int(bytes, ref offset);
                int index = network_utils.decode_int(bytes, ref offset);
                int serial_length = length - (offset - start);
                byte[] serialization = new byte[serial_length];
                System.Buffer.BlockCopy(bytes, offset, serialization, 0, serial_length);
                try_get_rep(id)?.on_network_variable_change(client, index, serialization);
                break;

            case global::client.MESSAGE.TRIGGER:

                // Trigger the numbered network event on the given representation
                int network_id = network_utils.decode_int(bytes, ref offset);
                int event_number = network_utils.decode_int(bytes, ref offset);
                if (representations.TryGetValue(network_id, out representation rep))
                    rep.trigger_network_event(client, event_number);
                else
                    log_warning("Reccived trigger for non-existant network ID!");
                break;

            case global::client.MESSAGE.RENDER_RANGE_UPDATE:

                client.render_range = network_utils.decode_float(bytes, ref offset);
                break;

            case global::client.MESSAGE.CREATE:

                // Create the representation from the info sent from the client
                int input_id;
                rep = representation.create(bytes, offset, length, out input_id);
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
                send_message(MESSAGE.CREATION_SUCCESS, client, input_id, rep.network_id);

                // Register (load) the object on clients
                client.load(rep, true);

                // If this is a child, load it on all other clients that have the parent.
                if (rep.parent != null && rep.parent is representation)
                    foreach (var c in connected_clients)
                        if (c != client)
                            if (c.has_loaded((representation)rep.parent))
                                c.load(rep, false);
                break;

            case global::client.MESSAGE.DELETE:

                network_id = network_utils.decode_int(bytes, ref offset);
                bool response = network_utils.decode_bool(bytes, ref offset);

                // Find the representation being deleted
                representation deleting;
                if (!representations.TryGetValue(network_id, out deleting))
                {
                    if (!recently_deleted.ContainsKey(network_id))
                    {
                        // This should only happend in high-latency edge cases
                        log_warning("Deleting non-existant id " + network_id + " (was not recently deleted)\n");
                    }

                    return;
                }

                // Delete the representation
                deleting.delete(issued_from: client, response_requested: response);
                break;

            case global::client.MESSAGE.KICK:

                // Disconnect the requested player
                string username = network_utils.decode_string(bytes, ref offset);
                foreach (var c in connected_clients)
                    if (c.username == username)
                    {
                        c.disconnect("kicked by " + client.username);
                        break;
                    }
                break;

            case global::client.MESSAGE.JOINABLE_QUERY:

                // Let the client know if I'm joinable
                bool joinable = true;
                send_message(MESSAGE.JOINABLE_RESPONSE, client, joinable);
                break;

            default:
                throw new System.Exception("Unkown message type!");
        };
    }

    //##############//
    // VERSION INFO //
    //##############//

#if STANDALONE_SERVER
    public static string version => standalone_server.version;
#else
    public static string version => version_control.version;
#endif

    //#########//
    // LOGGING //
    //#########//

#if STANDALONE_SERVER
    static Queue<string> log_queue = new Queue<string>();
    public static string pop_log_queue() => log_queue.Count == 0 ? null : log_queue.Dequeue();
    public static void log<T>(T message) => log_queue.Enqueue(message.ToString());
    public static void log_error<T>(T message) => log_queue.Enqueue("ERROR: " + message.ToString());
    public static void log_warning<T>(T message) => log_queue.Enqueue("Warning: " + message.ToString());
#else
    public static void log<T>(T message) => Debug.Log(message);
    public static void log_error<T>(T message) => Debug.LogError(message);
    public static void log_warning<T>(T message) => Debug.LogWarning(message);
#endif

#if STANDALONE_SERVER

    //#######################################//
    // Standalone-server versions of various //
    // things that rely on the unity engine. //
    //#######################################//

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public float sqrMagnitude { get => x * x + y * y + z * z; }
        public float magnitude { get => (float)System.Math.Sqrt(sqrMagnitude); }

        // STATIC METHODS //

        public static Vector3 operator -(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
        }

        public static Vector3 zero
        {
            get => new Vector3(0, 0, 0);
        }

        public override string ToString()
        {
            return string.Format("[{0},{1},{2}]", x.ToString("F2"), y.ToString("F2"), z.ToString("F2"));
        }
    }

    public static class Time
    {
        static System.DateTime start_time
        {
            get
            {
                if (!start_time_calculated)
                {
                    _start_time = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
                    start_time_calculated = true;
                }
                return _start_time;
            }
        }
        static System.DateTime _start_time;
        static bool start_time_calculated = false;

        public static float realtimeSinceStartup
        {
            get => (float)(System.DateTime.UtcNow - start_time).TotalSeconds;
        }

    }

    static class Application
    {
        public static string persistentDataPath
        {
            get => System.Environment.CurrentDirectory;
        }
    }

    class network_lookup
    {
        public network_lookup(float radius, bool persist)
        {
            this.radius = radius;
            this.persist = persist;
        }

        float radius;
        bool persist;

        public float network_radius() { return radius; }
        public bool persistant() { return persist; }
    }

    static class networked
    {
        static Dictionary<string, network_lookup> data;

        public static network_lookup look_up(string path)
        {
            if (data == null)
            {
                data = new Dictionary<string, network_lookup>();
                foreach (var s in System.IO.File.ReadAllLines("server_data"))
                {
                    var splt = s.Split();
                    if (splt.Length != 3) throw new System.Exception("Incorrect entry length in server_data!");
                    data[splt[0]] = new network_lookup(float.Parse(splt[1]), bool.Parse(splt[2]));
                }
            }

            if (!data.TryGetValue(path, out network_lookup ret))
                throw new System.Exception("Could not find entry in server_data: " + path);

            return ret;
        }
    }

#endif
}

/// <summary> Tag a networked variable with a predefined type, because 
/// it has a special meaning to the network engine. </summary>
public class engine_networked_variable : System.Attribute
{
    public enum TYPE : int
    {
        POSITION_X,
        POSITION_Y,
        POSITION_Z,
    }

    public TYPE type;
    public engine_networked_variable(TYPE type) { this.type = type; }
}