#if FACEPUNCH_STEAMWORKS

using System.Collections.Generic;
using System;

public class steamworks_stream : backend_stream
{
    Queue<byte[]> packets = new Queue<byte[]>();
    public override bool DataAvailable => packets.Count > 0;
    public override bool CanRead => true;

    Steamworks.SteamId write_id;
    int write_channel;

    public steamworks_stream(Steamworks.SteamId write_id, int write_channel)
    {
        this.write_id = write_id;
        this.write_channel = write_channel;
    }

    public override int Read(byte[] buffer, int start, int count)
    {
        int offset = start;
        int left_to_read = count;
        int total_read = 0;

        while (left_to_read > 0 && packets.Count > 0)
        {
            // Get the next P2P packet + how many bytes we want from it
            var packet = packets.Dequeue();
            int bytes_from_packet = Math.Min(packet.Length, left_to_read);

            // Copy those bytes to the buffer
            Buffer.BlockCopy(packet, 0, buffer, offset, bytes_from_packet);

            // Advance the offset
            offset += bytes_from_packet;
            left_to_read -= bytes_from_packet;
            total_read += bytes_from_packet;

            // We've read the whole packet
            if (bytes_from_packet == packet.Length)
                continue;

            // We've only read a partial packet, create
            // a new queue with the remainder of this packet at the start.
            // This is reasonably expensive, but shouldn't happen that often.
            var new_packets = new Queue<byte[]>();
            byte[] remainder = new byte[packet.Length - bytes_from_packet];
            Buffer.BlockCopy(packet, bytes_from_packet, remainder, 0, remainder.Length);

            // Add remainder + copy rest of queue
            new_packets.Enqueue(remainder);
            while (packets.Count > 0)
                new_packets.Enqueue(packets.Dequeue());
            packets = new_packets;
        }

        return total_read;
    }

    public override void Write(byte[] buffer, int start, int count)
    {
        // Send data directly over P2P
        byte[] to_send = new byte[count];
        Buffer.BlockCopy(buffer, start, to_send, 0, count);
        Steamworks.SteamNetworking.SendP2PPacket(write_id, to_send, to_send.Length, nChannel: write_channel);
    }

    public void ReceiveP2Ppacket(byte[] data)
    {
        packets.Enqueue(data);
    }

    public override void Close(int timeout_ms) { }
}

public class steamworks_client_backend : client_backend
{
    public override int SendBufferSize => 16384;
    public override int ReceiveBufferSize => 16384;
    public override string remote_address => "Steamworks P2P client (steam id = " + Steamworks.SteamClient.SteamId.Value + ")";

    steamworks_stream steam_stream;
    public override backend_stream stream => steam_stream;
    public void ReceiveP2Ppacket(byte[] data) => steam_stream.ReceiveP2Ppacket(data);
    public Steamworks.SteamId write_id { get; private set; }

    public steamworks_client_backend(Steamworks.SteamId write_id, int write_channel)
    {
        this.write_id = write_id;
        steam_stream = new steamworks_stream(write_id, write_channel);
    }

    public override void Update()
    {
        // Accept incoming data from the server
        while (Steamworks.SteamNetworking.IsP2PPacketAvailable(
            channel: steamworks_server_backend.SERVER_TO_CLIENT_CHANNEL))
        {
            var data = Steamworks.SteamNetworking.ReadP2PPacket(
                channel: steamworks_server_backend.SERVER_TO_CLIENT_CHANNEL);

            steam_stream.ReceiveP2Ppacket(data.Value.Data);
        }
    }

    public override bool has_disconnected => p2p_failure;
    bool p2p_failure = false;

    public void on_P2P_failure()
    {
        p2p_failure = true;
    }
}

public class steamworks_server_backend : server_backend
{
    public const int SERVER_TO_CLIENT_CHANNEL = 0;
    public const int CLIENT_TO_SERVER_CHANNEL = 1;

    /// <summary> Steam P2P connections awaiting accept. </summary>
    Dictionary<Steamworks.SteamId, steamworks_client_backend> pending_users =
        new Dictionary<Steamworks.SteamId, steamworks_client_backend>();

    /// <summary> Accepted steam P2P connections. </summary>
    Dictionary<Steamworks.SteamId, steamworks_client_backend> accepted_users =
        new Dictionary<Steamworks.SteamId, steamworks_client_backend>();

    bool already_seen(Steamworks.SteamId id) => pending_users.ContainsKey(id) || accepted_users.ContainsKey(id);

    public override void Start()
    {
        Steamworks.SteamNetworking.OnP2PSessionRequest = (id) =>
        {
            // Accept all incoming connections
            Steamworks.SteamNetworking.AcceptP2PSessionWithUser(id);
            pending_users[id] = new steamworks_client_backend(id, SERVER_TO_CLIENT_CHANNEL);
        };

        Steamworks.SteamNetworking.OnP2PConnectionFailed = (id, err) =>
        {
            steamworks_client_backend client = null;
            if (!accepted_users.TryGetValue(id, out client))
                if (!pending_users.TryGetValue(id, out client))
                    return;

            client.on_P2P_failure();
        };
    }

    public override void Update()
    {
        // Redirect incoming P2P messages to client buffers
        while (Steamworks.SteamNetworking.IsP2PPacketAvailable(channel: CLIENT_TO_SERVER_CHANNEL))
        {
            var data = Steamworks.SteamNetworking.ReadP2PPacket(channel: CLIENT_TO_SERVER_CHANNEL);

            steamworks_client_backend client = null;
            if (!accepted_users.TryGetValue(data.Value.SteamId, out client))
                if (!pending_users.TryGetValue(data.Value.SteamId, out client))
                {
                    // We've got a message from a client that we haven't seen yet
                    // Somehow they skipped the OnP2PSessionRequest (perhaps because
                    // the P2P session was still active from a previous connection).
                    // No bother, we just queue them for a pending accept.
                    client = new steamworks_client_backend(data.Value.SteamId, SERVER_TO_CLIENT_CHANNEL);
                    if (!already_seen(data.Value.SteamId))
                        pending_users[data.Value.SteamId] = client;
                }

            client.ReceiveP2Ppacket(data.Value.Data);
        }
    }

    public override void on_disconnect(client_backend client)
    {
        if (client is steamworks_client_backend)
        {
            var steam_client = (steamworks_client_backend)client;
            var id = steam_client.write_id;
            pending_users.Remove(id);
            accepted_users.Remove(id);

            // Close the session (unless it is with myself - this would cause an error)
            if (id != Steamworks.SteamClient.SteamId)
                Steamworks.SteamNetworking.CloseP2PSessionWithUser(id);
        }
    }

    public override bool Pending() => pending_users.Count > 0;

    public override client_backend AcceptClient()
    {
        // Accept the first client from the pending users dictionary.
        // In the process, move them from the pending users dictionary
        // to the accepted users dictionary.
        foreach (var kv in pending_users)
        {
            if (accepted_users.ContainsKey(kv.Key))
                throw new Exception("Tried to accept already-accepted user!");

            pending_users.Remove(kv.Key);
            accepted_users[kv.Key] = kv.Value;
            return kv.Value;
        }

        throw new Exception("No pending steamworks P2P connections to accept!");
    }

    public override string local_address =>
        "Steam P2P connections (" + pending_users.Count + " pending, " + accepted_users.Count + " accepted)" +
        " id = " + Steamworks.SteamClient.SteamId.Value;
}

#endif // FACEPUNCH_STEAMWORKS