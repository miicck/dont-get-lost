using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public static class world
{
    // The name this world is (to be) saved as
    public static string name = "world";

    // The folder to save the world in
    public static string save_folder()
    {
        // First, ensure the folder exists 
        string folder = Application.persistentDataPath + "/worlds/" + name;
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;
}