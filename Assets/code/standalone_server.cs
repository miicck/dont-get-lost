using System;
using System.IO;
using System.Threading;

#if STANDALONE_SERVER
public static class standalone_server
{
    public static void Main(string[] args)
    {
        // Clear the log
        if (File.Exists("log"))
            File.Delete("log");

        // Check args
        if (args.Length < 1)
        {
            log("Please provide the savename.");
            return;
        }

        // Attempt to start the server
        if (!server.start(server.DEFAULT_PORT, args[0], "misc/player", out string err))
        {
            log("Error: The server failed to start: "+err);
            return;
        }
        else log("Started server (version "+version+")");

        // Run server updates, processing commands from
        // the cmd file + logging to the log file
        while(true)
        {
            server.update();
            Thread.Sleep(17);
            if (!server.started) break;

            while(true)
            {
                string to_log = server.pop_log_queue();
                if (to_log == null) break;
                to_log = to_log.Trim();
                if (to_log.Length == 0) continue;
                log(to_log);
            }

            if (File.Exists("cmd"))
            {
                log(process_command(File.ReadAllText("cmd")));
                File.Delete("cmd");
            }
        }
    }

    // Process a standalone server command
    static string process_command(string cmd)
    {
        if (cmd == null) return null;
        cmd = cmd.Trim();

        if (cmd == "stop")
        {
            server.stop();
            return "Stopping server...";
        }

        if (cmd == "info")
            return server.info();

        return null;
    }

    // Log a message to the log file
    static void log<T>(T message)
    {
        using (var sw = File.AppendText("log"))
            sw.WriteLine(DateTime.Now+": "+message);
    }

    // The server version name
    public static string version => gen_server_data.version.Trim();
}
#endif
