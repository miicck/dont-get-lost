using System.Collections;
using System.Collections.Generic;

#if STANDALONE_SERVER
public static class standalone_server
{
    public static void Main(string[] args)
    {
        server.start(server.DEFAULT_PORT, args[0], "misc/player", out string err);

        System.Console.CancelKeyPress += new System.ConsoleCancelEventHandler(stop);

        while (true)
        {
            server.update();
            if (!server.started)
                break;
        }
    }

    static void stop(object sender, System.ConsoleCancelEventArgs args)
    {
        server.stop();
    }
}
#endif
