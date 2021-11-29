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
        var backends = new List<server_backend>
        {
            new local_server_backend(),
            new tcp_server_backend(network_utils.local_ip_address(), server.DEFAULT_PORT)
        };

#if FACEPUNCH_STEAMWORKS
        backends.Add(new steamworks_server_backend());
#endif

        return new combined_server_backend(backends);
    }
}

/// <summary> A server backend composed of 
/// multiple communication channels. </summary>
public class combined_server_backend : server_backend
{
    IEnumerable<server_backend> backends;

    public combined_server_backend(IEnumerable<server_backend> backends)
    {
        this.backends = backends;
    }

    public override void Start()
    {
        foreach (var b in backends)
            b.Start();
    }

    public override void Stop()
    {
        foreach (var b in backends)
            b.Stop();
    }

    public override client_backend AcceptClient()
    {
        foreach (var b in backends)
            if (b.Pending())
                return b.AcceptClient();

        throw new System.Exception("No backend has pending clients!");
    }

    public override bool Pending()
    {
        foreach (var b in backends)
            if (b.Pending())
                return true;
        return false;
    }

    public override string local_address
    {
        get
        {
            string ret = "Multiple backends\n";
            foreach (var b in backends)
                ret += "    " + b.local_address + "\n";
            return ret.Trim();
        }
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
    public override client_backend AcceptClient() => new tcp_client_backend(listener.AcceptTcpClient());
    public override string local_address
    {
        get
        {
            var ip = (IPEndPoint)listener.LocalEndpoint;
            return ip.Address + ", port " + ip.Port + " (TCP)";
        }
    }
}

/// <summary> A backend for communication with a client on the local machine. </summary>
public class local_server_backend : server_backend
{
    bool accepted_local_client = false;

    public override void Start() { }
    public override void Stop() { }

    public override string local_address => "Local server";

    public override bool Pending() => local_client_backend.local_server_client != null && !accepted_local_client;

    public override client_backend AcceptClient()
    {
        if (accepted_local_client)
            throw new System.Exception("Local client already accepted!");

        accepted_local_client = true;

        return local_client_backend.local_server_client;
    }
}