using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Wrapper class for steam-related stuff. </summary>
#if FACEPUNCH_STEAMWORKS
public static class steam
{
    public static void start()
    {
        try
        {
            Steamworks.SteamClient.Init(1442360);
            Debug.Log("Stated steam client for " + Steamworks.SteamClient.Name);
        }
        catch { Debug.Log("Failed to initialize steamworks client!"); }
    }

    public static void update()
    {
        Steamworks.SteamClient.RunCallbacks();
    }

    public static void stop()
    {
        try { Steamworks.SteamClient.Shutdown(); }
        catch { }
    }

    public static string username()
    {
        try { return Steamworks.SteamClient.Name; }
        catch { return PlayerPrefs.GetString("username"); }
    }

    public static bool connected
    {
        get
        {
            try { return Steamworks.SteamClient.IsLoggedOn; }
            catch { return false; }
        }
    }

    public static ulong steam_id
    {
        get
        {
            try { return Steamworks.SteamClient.SteamId.Value; }
            catch { return 0; }
        }
    }

    public static bool file_exists(string filename)
    {
        if (!connected) return false;
        try { return Steamworks.SteamRemoteStorage.FileExists(filename); }
        catch { return false; }
    }

    public static bool save_file(string filename, byte[] data)
    {
        if (!connected) return false;
        try { return Steamworks.SteamRemoteStorage.FileWrite(filename, data); }
        catch { return false; }
    }

    public static byte[] load_file(string filename)
    {
        if (!connected) return null;
        try { return Steamworks.SteamRemoteStorage.FileRead(filename); }
        catch { return null; }
    }

    public static bool delete_file(string filename)
    {
        if (!connected) return false;
        try { return Steamworks.SteamRemoteStorage.FileDelete(filename); }
        catch { return false; }
    }

    public static IEnumerable<string> list_files()
    {
        if (!connected) return new List<string> { };
        try { return Steamworks.SteamRemoteStorage.Files; }
        catch { return new List<string> { }; }
    }
}

#else // Not using Facepunch.Steamworks
public static class steam
{
    public static void start() { }
    public static void update() { }
    public static void stop() { }
    public static string username() => PlayerPrefs.GetString("username");
    public static bool connected => false;
    public static ulong steam_id => 0;
    public static bool file_exists(string filename) => false;
    public static bool save_file(string filename, byte[] data) => false;
    public static byte[] load_file(string filename) => null;
    public static bool delete_file(string filename) => false;
    public static IEnumerable<string> list_files() => new List<string> { };
}
}
#endif