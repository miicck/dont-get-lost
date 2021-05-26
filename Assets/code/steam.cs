#define FACEPUNCH_STEAMWORKS // Use the Facepunch.Steamworks steamworks C# wrapper
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
}
#endif