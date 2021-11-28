using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public abstract class server_backend
{
    public abstract void Start();
    public abstract void Stop();
    public abstract bool Pending();
    public abstract client_backend AcceptClient();
    public abstract EndPoint LocalEndpoint { get; }
}

public class tcp_server_backend : server_backend
{
    TcpListener listener;

    public tcp_server_backend(IPAddress address, int port)
    {
        listener = new TcpListener(address, port);
    }

    public override void Start() => listener.Start();
    public override void Stop() => listener.Stop();
    public override bool Pending() => listener.Pending();
    public override EndPoint LocalEndpoint => listener.LocalEndpoint;
    public override client_backend AcceptClient() => new tcp_client_backend(listener.AcceptTcpClient());
}