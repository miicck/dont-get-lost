using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public abstract class backend_stream
{
    public abstract bool CanRead { get; }
    public abstract bool DataAvailable { get; }
    public abstract int Read(byte[] buffer, int start, int count);
    public abstract void Write(byte[] buffer, int start, int count);
    public abstract void Close(int timeout_ms);
}

public class tcp_stream : backend_stream
{
    NetworkStream net_stream;

    public tcp_stream(NetworkStream tcp_net_stream)
    {
        net_stream = tcp_net_stream;
    }

    public override bool CanRead => net_stream.CanRead;
    public override bool DataAvailable => net_stream.DataAvailable;
    public override int Read(byte[] buffer, int start, int count) => net_stream.Read(buffer, start, count);
    public override void Write(byte[] buffer, int start, int count) => net_stream.Write(buffer, start, count);
    public override void Close(int timeout_ms) => net_stream.Close(timeout_ms);
}

public abstract class client_backend
{
    public abstract void Close(int timeout_ms = 0);
    public abstract backend_stream stream { get; }
    public abstract int ReceiveBufferSize { get; }
    public abstract int SendBufferSize { get; }
    public abstract IAsyncResult BeginConnect(string address, int port);
    public abstract string remote_address { get; }

    public static client_backend default_backend()
    {
        return new tcp_client_backend();
        return new steamworks_client_backend();
    }
}

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

    public override void Close(int timeout_ms)
    {

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

public class steamworks_client_backend : client_backend
{
    public Steamworks.SteamId id { get; private set; }
    bool server_side;

    public steamworks_client_backend(Steamworks.SteamId steamId, bool server_side = false)
    {
        id = steamId;
        this.server_side = server_side;

        if (!server_side)
            steamworks_server_backend.register_local_client(this);
    }

    public steamworks_client_backend() : this(Steamworks.SteamClient.SteamId) { }

    public override IAsyncResult BeginConnect(string address, int port)
    {
        return null;
    }

    public override void Close(int timeout_ms = 0)
    {

    }

    public override int ReceiveBufferSize => 16384;
    public override int SendBufferSize => 16384;
    public override string remote_address => "Steam user " + id.Value;

    public override backend_stream stream
    {
        get
        {
            if (_steamworks_stream == null)
            {
                int send_channel = server_side ? 0 : 1;
                int receive_channel = server_side ? 1 : 0;

                _steamworks_stream = new steamworks_stream(id, send_channel, receive_channel);
            }
            return _steamworks_stream;
        }
    }
    steamworks_stream _steamworks_stream;
}