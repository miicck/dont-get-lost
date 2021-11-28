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
}

public class tcp_client_backend : client_backend
{
    TcpClient client;

    public tcp_client_backend(TcpClient client)
    {
        this.client = client;
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
}
