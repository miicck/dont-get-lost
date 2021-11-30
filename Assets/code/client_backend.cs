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
    public void Close(int timeout_ms = 0)
    {
        this.OnClose();
        stream.Close(timeout_ms);
    }

    /// <summary> Callback when client backend is closed. </summary>
    protected virtual void OnClose() { }

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

        // Create the client that the server will see (i.e the other end of my stream)
        local_server_client = new local_client_backend(local_stream.other_end);
    }

    private local_client_backend(local_stream local_stream)
    {
        this.local_stream = local_stream;
    }

    protected override void OnClose()
    {
        local_server_client = null;
    }

    public override backend_stream stream => local_stream;
    public override int SendBufferSize => 16384;
    public override int ReceiveBufferSize => 16384;
    public override string remote_address =>
        "Local stream (capacities up/down = " +
        local_stream.other_end.capacity + "/" +
        local_stream.capacity + ")";

    //##############//
    // STATIC STUFF //
    //##############//

    public static local_client_backend local_server_client { get; private set; }
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

        if (connector.AsyncWaitHandle.WaitOne(global::client.CONNECTION_TIMEOUT_MS))
            return new tcp_client_backend(client);

        return null;
    }
}