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
        catch (System.Exception e)
        {
            Debug.LogError("Failed to initialize steamworks client!");
        }
    }

    public static void update()
    {
        Steamworks.SteamClient.RunCallbacks();
    }

    public static void stop()
    {
        try
        {
            Steamworks.SteamClient.Shutdown();
        }
        catch (System.Exception e)
        {

        }
    }
}

#else // Not using Facepunch.Steamworks
public static class steam
{
    public static void start() { }
    public static void update() { }
    public static void stop() { }
}
#endif