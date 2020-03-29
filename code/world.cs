using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public static class world
{
    // The seed for the world generator
    public static int seed;

    // The name this world is (to be) saved as
    public static string name = "world";

    // The folder to save the world in
    public static string save_folder()
    {
        // First, ensure the folder exists 
        string folder = worlds_folder() + name;
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    // The folder where all the worlds are stored
    public static string worlds_folder()
    {
        return Application.persistentDataPath + "/worlds/";
    }

    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;
}