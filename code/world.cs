using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public class world : networked.section
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    // The seed for the world generator
    public static int seed { get; private set; }

    // Returns true if the world details have been loaded from the server
    public static bool loaded { get => seed != 0; }

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

    public override byte[] section_id_bytes()
    {
        // There is only one world section, and it is always the same
        return new byte[] { };
    }

    public override void section_id_initialize(params object[] section_id_init_args)
    {
        seed = (int)section_id_init_args[0];
        name = (string)section_id_init_args[1];
        if (server.started && seed == 0)
            throw new System.Exception("Invalid seed!");
    }

    protected override byte[] serialize()
    {
        // Serialize the seed, and name
        byte[] name_bytes = System.Text.Encoding.ASCII.GetBytes(name);
        return concat_buffers(
            System.BitConverter.GetBytes(seed),
            System.BitConverter.GetBytes(name_bytes.Length),
            name_bytes
        );
    }

    protected override void deserialize(byte[] bytes, int offset, int count)
    {
        // Deserialize the seed and string bytes length 
        seed = System.BitConverter.ToInt32(bytes, offset);
        int string_length = System.BitConverter.ToInt32(bytes, offset + sizeof(int));

        // Copy the name bytes to a temporary array, and deserialize the name
        byte[] name_bytes = new byte[string_length];
        System.Buffer.BlockCopy(bytes, offset + sizeof(int) * 2, name_bytes, 0, string_length);
        name = System.Text.Encoding.ASCII.GetString(name_bytes);
    }

    protected override void on_first_sync()
    {
        // Create the player
        player.create("mick");

        // Create the first biome
        var biome_coords = biome.coords(Vector3.zero);
        biome.generate(biome_coords[0], biome_coords[1]);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(world))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            world w = (world)target;
            UnityEditor.EditorGUILayout.IntField("Seed", seed);
            UnityEditor.EditorGUILayout.TextField("Name", name);
        }
    }
#endif
}