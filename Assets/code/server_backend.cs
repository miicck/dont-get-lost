using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary> The interface needed for the server to communicate with clients. </summary>
public abstract class server_backend
{
    /// <summary> Start listening for incomming messages (start the server). </summary>
    public abstract void Start();

    /// <summary> Stop listening to messages (stop the server). </summary>
    public abstract void Stop();

    /// <summary> Are there pending incomming connections? </summary>
    /// <returns>True if there are pending incoming connections.</returns>
    public abstract bool Pending();

    /// <summary> Call to accept a pending incomming connection. </summary>
    /// <returns>The client that was trying to connect.</returns>
    public abstract client_backend AcceptClient();

    /// <summary> A string describing the address that this backend is listening on. </summary>
    public abstract string local_address { get; }

    //##############//
    // STATIC STUFF //
    //##############//

    /// <summary> The default backend used. </summary>
    public static server_backend default_backend()
    {
        return new tcp_server_backend(network_utils.local_ip_address(), server.DEFAULT_PORT);
        return new steamworks_server_backend();
    }
}

/// <summary> A TCP implementation of the server backend. </summary>
public class tcp_server_backend : server_backend
{
    TcpListener listener;
    public tcp_server_backend(IPAddress address, int port) { listener = new TcpListener(address, port); }
    public override void Start() => listener.Start();
    public override void Stop() => listener.Stop();
    public override bool Pending() => listener.Pending();
    public override string local_address => listener.LocalEndpoint.ToString();
    public override client_backend AcceptClient() => new tcp_client_backend(listener.AcceptTcpClient());
}

/// <summary> A Steam P2P networking implementation of the server backend. </summary>
public class steamworks_server_backend : server_backend
{
    /// <summary> Remote steam users awaiting connection. </summary>
    Queue<Steamworks.SteamId> pending_users = new Queue<Steamworks.SteamId>();

    public override void Start()
    {
        if (!Steamworks.SteamClient.IsLoggedOn)
            throw new System.Exception("Steam not connected!");

        // Accept new P2P connections
        Steamworks.SteamNetworking.OnP2PSessionRequest = (new_id) =>
        {
            Steamworks.SteamNetworking.AcceptP2PSessionWithUser(new_id);
            pending_users.Enqueue(new_id);
        };
    }

    public override void Stop() { } // Nothing to do

    /// <summary> Either remote steam users are awaiting connection, 
    /// or the local client is awaiting connection. </summary>
    public override bool Pending() => pending_users.Count > 0 || local_client_awating_accept;

    public override client_backend AcceptClient()
    {
        if (local_client_awating_accept)
        {
            // Accept local client
            local_client_awating_accept = false;
            return local_client;
        }

        // Accept remote client
        var id = pending_users.Dequeue();
        return new steamworks_client_backend(id, server_side: true);
    }

    /// <summary> For steam P2P communication, the address is basically just the steam user. </summary>
    public override string local_address => "Steam user " + Steamworks.SteamClient.SteamId.Value;

    //##############//
    // STATIC STUFF //
    //##############//

    static steamworks_client_backend local_client = null;
    static bool local_client_awating_accept = false;

    /// <summary> Call to register a local steam user. This is neccassary because the local
    /// steam client does not send a P2PSessionRequest to itself, but the server
    /// still needs to accept the local client. </summary>
    public static void register_local_client(steamworks_client_backend client)
    {
        if (local_client != null)
            throw new System.Exception("Local client already registered!");

        local_client = new steamworks_client_backend(client.id, server_side: true);
        local_client_awating_accept = true;
    }
}