using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary> The byte stream interface needed for 
/// communication between the sever and clients. </summary>
public abstract class backend_stream
{
    /// <summary> True if this stream supports reading 
    /// (should basically always be the case, mainly included 
    /// to accomodate the NetworkStream equivalent). </summary>
    public abstract bool CanRead { get; }

    /// <summary> True if there is data waiting to be read from the stream. </summary>
    public abstract bool DataAvailable { get; }

    /// <summary> Read data from the stream. </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="start">The start location in the buffer to start writing to.</param>
    /// <param name="count">The maximum number of bytes that can be read into the buffer.</param>
    /// <returns>The number of bytes read from the stream.</returns>
    public abstract int Read(byte[] buffer, int start, int count);

    /// <summary> Write data to the stream. </summary>
    /// <param name="buffer">The buffer to write from.</param>
    /// <param name="start">The start location in the buffer to start from.</param>
    /// <param name="count">The number of bytes to write.</param>
    public abstract void Write(byte[] buffer, int start, int count);

    /// <summary> Close the stream. </summary>
    /// <param name="timeout_ms">The number of milliseconds to linger for before actually closing.</param>
    public abstract void Close(int timeout_ms);
}

/// <summary> TCP implementation of a <see cref="backend_stream"/>. </summary>
public class tcp_stream : backend_stream
{
    NetworkStream net_stream;
    public tcp_stream(NetworkStream tcp_net_stream) { net_stream = tcp_net_stream; }
    public override bool CanRead => net_stream.CanRead;
    public override bool DataAvailable => net_stream.DataAvailable;
    public override int Read(byte[] buffer, int start, int count) => net_stream.Read(buffer, start, count);
    public override void Write(byte[] buffer, int start, int count) => net_stream.Write(buffer, start, count);
    public override void Close(int timeout_ms) => net_stream.Close(timeout_ms);
}

/// <summary> The interface needed for the client to communicate with the server. </summary>
public abstract class client_backend
{
    /// <summary> Close the connection to the server. </summary>
    /// <param name="timeout_ms">The number of milliseconds to linger for before actually closing.</param>
    public abstract void Close(int timeout_ms = 0);

    /// <summary> The byte stream used to communicate with the server. </summary>
    public abstract backend_stream stream { get; }

    /// <summary> How many bytes should be allocated to store incoming messages. </summary>
    public abstract int ReceiveBufferSize { get; }

    /// <summary> How many bytes should be allocated to store outgoing messages. </summary>
    public abstract int SendBufferSize { get; }

    /// <summary> A string representing the address of this client (as seen by the server). </summary>
    public abstract string remote_address { get; }
}

/// <summary> A backend_stream implemented as a MemoryStream. </summary>
public class local_stream : backend_stream
{
    public override bool CanRead => true;
    public override void Close(int timeout_ms) { }

    public local_stream other_end
    {
        get
        {
            if (_other_end == null)
            {
                // Create and link the other end of the stream
                _other_end = new local_stream();
                _other_end._other_end = this;
            }
            return _other_end;
        }
    }
    local_stream _other_end;

    byte[] incoming_buffer = new byte[16384];
    int position = 0;

    public int capacity => incoming_buffer.Length;

    public override bool DataAvailable => position > 0;

    public override int Read(byte[] buffer, int start, int count)
    {
        int read = Math.Min(count, position);
        Buffer.BlockCopy(incoming_buffer, 0, buffer, start, read);

        // Shift remaining unread portion of buffer to start
        for (int i = 0; i <= position - read; ++i)
            incoming_buffer[i] = incoming_buffer[i + read];

        // Point to start of buffer
        position = 0;
        return read;
    }

    public override void Write(byte[] buffer, int start, int count)
    {
        // Write to the other end
        other_end.WriteToIncoming(buffer, start, count);
    }

    void WriteToIncoming(byte[] buffer, int start, int count)
    {
        while (position + count > incoming_buffer.Length)
        {
            // Make buffer bigger to accomodate message
            var new_buffer = new byte[incoming_buffer.Length * 2];
            Buffer.BlockCopy(incoming_buffer, 0, new_buffer, 0, incoming_buffer.Length);
            incoming_buffer = new_buffer;
        }

        Buffer.BlockCopy(buffer, start, incoming_buffer, position, count);
        position += count;
    }
}

public class local_client_backend : client_backend
{
    local_stream local_stream;

    public local_client_backend()
    {
        // Setup my local stream 
        local_stream = new local_stream();

        // Give the local server the other end of my local stream
        local_server_backend.local_client = new local_client_backend(local_stream.other_end);
    }

    private local_client_backend(local_stream local_stream)
    {
        this.local_stream = local_stream;
    }

    public override void Close(int timeout_ms = 0) { }
    public override backend_stream stream => local_stream;
    public override int SendBufferSize => 16384;
    public override int ReceiveBufferSize => 16384;
    public override string remote_address =>
        "Local stream (capacities up/down = " +
        local_stream.other_end.capacity + "/" +
        local_stream.capacity + ")";
}

/// <summary> A TCP implementation of a <see cref="client_backend"/> </summary>
public class tcp_client_backend : client_backend
{
    TcpClient client;

    public tcp_client_backend(TcpClient client)
    {
        this.client = client;

        // Let the TCP connection linger after disconnect, so queued messages are sent
        client.LingerState = new LingerOption(true, 10);
    }

    public override backend_stream stream
    {
        get
        {
            if (_backend_stream == null)
                _backend_stream = new tcp_stream(client.GetStream());
            return _backend_stream;
        }
    }
    backend_stream _backend_stream;

    public override int ReceiveBufferSize => client.ReceiveBufferSize;
    public override int SendBufferSize => client.SendBufferSize;
    public override void Close(int timeout_ms = 0) => stream.Close(timeout_ms);

    public override string remote_address
    {
        get
        {
            var ip = (IPEndPoint)client.Client.RemoteEndPoint;
            return ip.Address + ":" + ip.Port;
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static tcp_client_backend connect(string host, int port)
    {
        var client = new TcpClient();
        var connector = client.BeginConnect(host, port, null, null);

        // Connection timeout
        if (!connector.AsyncWaitHandle.WaitOne(global::client.CONNECTION_TIMEOUT_MS))
            return null;

        return new tcp_client_backend(client);
    }
}

/// <summary> A Steamworks P2P-networking implemntation of a <see cref="backend_stream"/>. </summary>
class steamworks_stream : backend_stream
{
    Steamworks.SteamId id;
    int send_channel;
    int receive_channel;

    public steamworks_stream(Steamworks.SteamId steamId, int send_channel, int receive_channel)
    {
        id = steamId;
        this.send_channel = send_channel;
        this.receive_channel = receive_channel;

        // Accept incoming connection requests
        Steamworks.SteamNetworking.OnP2PSessionRequest = (new_id) =>
        {
            Steamworks.SteamNetworking.AcceptP2PSessionWithUser(new_id);
        };
    }

    public override bool CanRead => true;
    public override bool DataAvailable => Steamworks.SteamNetworking.IsP2PPacketAvailable(channel: receive_channel);

    public override void Close(int timeout_ms) { } // Nothing to do

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
    public Steamworks.SteamId id { get; private set; }
    bool server_side;

    public steamworks_client_backend(Steamworks.SteamId steamId, bool server_side = false)
    {
        id = steamId;
        this.server_side = server_side;

        // This is a client-side client (i.e not the client that the server sees)
        // we need to manually register it on the local server. In order to make this
        // insensitive to the order in which the local-client and local-server are started
        // we can't check if a local-server is running before doing this. This is fine
        // because this won't do anything if there is no server running to accept clients.
        if (!server_side)
            steamworks_server_backend.register_local_client(this);
    }

    public override void Close(int timeout_ms = 0) { }
    public override int ReceiveBufferSize => 16384;
    public override int SendBufferSize => 16384;
    public override string remote_address => "Steam user " + id.Value;

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

                _steamworks_stream = new steamworks_stream(id, send_channel, receive_channel);
            }
            return _steamworks_stream;
        }
    }
    steamworks_stream _steamworks_stream;
}