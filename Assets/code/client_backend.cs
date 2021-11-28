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

    /// <summary> Called to start connecting to a server at the given address/port combo. </summary>
    public abstract IAsyncResult BeginConnect(string address, int port);

    /// <summary> A string representing the address of this client (as seen by the server). </summary>
    public abstract string remote_address { get; }

    /// <summary> The default backend used. </summary>
    public static client_backend default_backend()
    {
        return new tcp_client_backend();
        return new steamworks_client_backend();
    }
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

    public tcp_client_backend() : this(new TcpClient()) { }

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
    public override IAsyncResult BeginConnect(string address, int port) => client.BeginConnect(address, port, null, null);

    public override string remote_address
    {
        get
        {
            var ip = (IPEndPoint)client.Client.RemoteEndPoint;
            return ip.Address + ":" + ip.Port;
        }
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

    public steamworks_client_backend() : this(Steamworks.SteamClient.SteamId) { }
    public override IAsyncResult BeginConnect(string address, int port) { return null; }
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