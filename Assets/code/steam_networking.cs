#if FACEPUNCH_STEAMWORKS

using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary> A Steamworks P2P-networking implemntation of a <see cref="backend_stream"/>. </summary>
class steamworks_stream : backend_stream
{
    Steamworks.SteamId id;
    int send_channel;
    int receive_channel;

    HashSet<Steamworks.SteamId> all_accepted_sessions = new HashSet<Steamworks.SteamId>();

    public int accepted_P2P_sessions => all_accepted_sessions.Count;

    public steamworks_stream(Steamworks.SteamId steamId, int send_channel, int receive_channel)
    {
        id = steamId;
        this.send_channel = send_channel;
        this.receive_channel = receive_channel;

        // Accept incoming connection requests
        Steamworks.SteamNetworking.OnP2PSessionRequest = (new_id) =>
        {
            Steamworks.SteamNetworking.AcceptP2PSessionWithUser(new_id);
            all_accepted_sessions.Add(new_id);
            Debug.Log("Client accepted remote steamworks client " + new_id.Value);
        };
    }

    public override bool CanRead => true;
    public override bool DataAvailable => Steamworks.SteamNetworking.IsP2PPacketAvailable(channel: receive_channel);

    public override void Close(int timeout_ms)
    {
        // Close all P2P sessions
        foreach (var id in all_accepted_sessions)
            Steamworks.SteamNetworking.CloseP2PSessionWithUser(id);
        all_accepted_sessions.Clear();
    }

    public override int Read(byte[] buffer, int start, int count)
    {
        var packet = Steamworks.SteamNetworking.ReadP2PPacket(channel: receive_channel);
        var data = packet.Value.Data;

        if (data.Length > count)
            throw new Exception("Steamworks P2P packet is too large for buffer!");

        Buffer.BlockCopy(data, 0, buffer, start, data.Length);
        return data.Length;
    }

    public override void Write(byte[] buffer, int start, int count)
    {
        var data = new byte[count];
        Buffer.BlockCopy(buffer, start, data, 0, count);
        Steamworks.SteamNetworking.SendP2PPacket(id, data, nChannel: send_channel);
    }
}

/// <summary> A steamworks P2P-networking implementation of a <see cref="client_backend"/>. </summary>
public class steamworks_client_backend : client_backend
{
    public Steamworks.SteamId id_connected_to { get; private set; }
    bool server_side;

    public steamworks_client_backend(Steamworks.SteamId id_to_connect_to, bool server_side = false)
    {
        id_connected_to = id_to_connect_to;
        this.server_side = server_side;

        // If this isn't a server-side client (i.e the one the server uses to talk to clients)
        // then we need to create a server-side client for a local server to potentially use.
        // This is because steam does not trigger a OnP2PSessionRequest for a steam user trying
        // to send themselves messages.
        if (!server_side) local_server_client = new steamworks_client_backend(id_to_connect_to, server_side: true);
    }

    protected override void OnClose()
    {
        // Forget the local server client
        local_server_client = null;
    }

    public override int ReceiveBufferSize => 16384;
    public override int SendBufferSize => 16384;
    public override string remote_address =>
        "Steam user " + id_connected_to.Value + " (" + accepted_P2P_sessions + " P2P connections)" +
        (local_server_client == null ? "" : " + local server client");

    public override backend_stream stream
    {
        get
        {
            if (_steamworks_stream == null)
            {
                // Data from the server to a client is sent on channel 0
                // Data from a client to the server is sent on channel 1
                // This is so messages from a local-server to a local-client
                // (i.e messages from the server to itself) aren't mixed up
                int send_channel = server_side ? 0 : 1;
                int receive_channel = server_side ? 1 : 0;

                _steamworks_stream = new steamworks_stream(id_connected_to, send_channel, receive_channel);
            }
            return _steamworks_stream;
        }
    }
    steamworks_stream _steamworks_stream;

    public int accepted_P2P_sessions => _steamworks_stream == null ? 0 : _steamworks_stream.accepted_P2P_sessions;

    //##############//
    // STATIC STUFF //
    //##############//

    public static steamworks_client_backend local_server_client { get; private set; }
}

/// <summary> A Steam P2P networking implementation of the server backend. </summary>
public class steamworks_server_backend : server_backend
{
    HashSet<Steamworks.SteamId> pending_users = new HashSet<Steamworks.SteamId>();
    Dictionary<Steamworks.SteamId, steamworks_client_backend> accepted_users =
        new Dictionary<Steamworks.SteamId, steamworks_client_backend>();

    bool is_connected(Steamworks.SteamId id)
    {
        return accepted_users.ContainsKey(id) || pending_users.Contains(id);
    }

    void connect_user(Steamworks.SteamId id)
    {
        Steamworks.SteamNetworking.AcceptP2PSessionWithUser(id);
        pending_users.Add(id);
    }

    void disconnect_user(Steamworks.SteamId id)
    {
        // Only close the P2P session if it is a remote steam id
        // (if I try to close a P2P session with myself, it will crash)
        if (id != Steamworks.SteamClient.SteamId)
            Steamworks.SteamNetworking.CloseP2PSessionWithUser(id);

        pending_users.Remove(id);
        accepted_users.Remove(id);
    }

    steamworks_client_backend accept_user(Steamworks.SteamId id, steamworks_client_backend client)
    {
        pending_users.Remove(id);
        accepted_users[id] = client;
        return client;
    }

    bool local_client_accepted = false;

    public override void Start()
    {
        if (!Steamworks.SteamClient.IsLoggedOn)
            throw new Exception("Steam not connected!");

        // Accept new P2P connections
        Steamworks.SteamNetworking.OnP2PSessionRequest = connect_user;
    }

    public override void Stop()
    {
        // Disconnect all users
        var all_users = new HashSet<Steamworks.SteamId>();
        all_users.UnionWith(accepted_users.Keys);
        all_users.UnionWith(pending_users);

        foreach (var u in all_users)
            disconnect_user(u);
    }

    public override void on_disconnect(client_backend client)
    {
        if (client is steamworks_client_backend)
        {
            var steam_client = (steamworks_client_backend)client;
            disconnect_user(steam_client.id_connected_to);
        }
    }

    /// <summary> Either remote steam users are awaiting connection, 
    /// or the local client is awaiting connection. </summary>
    public override bool Pending() => pending_users.Count > 0 || local_client_pending;
    bool local_client_pending => steamworks_client_backend.local_server_client != null && !local_client_accepted;

    public override client_backend AcceptClient()
    {
        if (local_client_pending)
        {
            if (local_client_accepted)
                throw new Exception("Local steamworks client already accepted!");

            // Accept local client
            local_client_accepted = true;
            Debug.Log("Server accepted local steamworks client");

            // Accept the local client (return it)
            return accept_user(Steamworks.SteamClient.SteamId, steamworks_client_backend.local_server_client);
        }

        // Accept remote client
        Steamworks.SteamId? to_accept = null;
        foreach (var id in pending_users)
        {
            to_accept = id;
            break;
        }

        if (to_accept == null)
            throw new Exception("No pending users to accept!");

        Debug.Log("Server accepted remote steamworks client " + to_accept.Value.Value);
        return accept_user(to_accept.Value, new steamworks_client_backend(to_accept.Value, server_side: true));
    }

    /// <summary> For steam P2P communication, the address is basically just the steam user. </summary>
    public override string local_address => Steamworks.SteamClient.SteamId.Value +
        " (Steam P2P, " + accepted_users.Count + "/" + pending_users.Count + " accepted/pending users)" +
         (local_client_accepted ? " + local client" : "");
}

#endif // FACEPUNCH_STEAMWORKS