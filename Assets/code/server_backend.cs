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
    public abstract string local_address { get; }

    public static server_backend default_backend()
    {
        return new tcp_server_backend(network_utils.local_ip_address(), server.DEFAULT_PORT);
        return new steamworks_server_backend();
    }
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
    public override string local_address => listener.LocalEndpoint.ToString();
    public override client_backend AcceptClient() => new tcp_client_backend(listener.AcceptTcpClient());
}

public class steamworks_server_backend : server_backend
{
    Queue<Steamworks.SteamId> pending_users = new Queue<Steamworks.SteamId>();

    public override void Start()
    {
        if (!Steamworks.SteamClient.IsLoggedOn)
            throw new System.Exception("Steam not connected!");

        Steamworks.SteamNetworking.OnP2PSessionRequest = (new_id) =>
        {
            Steamworks.SteamNetworking.AcceptP2PSessionWithUser(new_id);
            pending_users.Enqueue(new_id);
        };
    }

    public override void Stop()
    {

    }

    public override bool Pending() => pending_users.Count > 0 || local_client_awating_accept;

    public override client_backend AcceptClient()
    {
        if (local_client_awating_accept)
        {
            local_client_awating_accept = false;
            return local_client;
        }

        var id = pending_users.Dequeue();
        return new steamworks_client_backend(id, server_side: true);
    }

    public override string local_address => "Steam user " + Steamworks.SteamClient.SteamId.Value;

    //##############//
    // STATIC STUFF //
    //##############//

    public static void register_local_client(steamworks_client_backend client)
    {
        if (local_client != null)
            throw new System.Exception("Local client already registered!");

        local_client = new steamworks_client_backend(client.id, server_side: true);
        local_client_awating_accept = true;
    }

    static steamworks_client_backend local_client = null;
    static bool local_client_awating_accept = false;
}