using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public class world : networked.section
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    // Returns true if the world details have been loaded from the server
    public static bool loaded { get => seed != 0; }

    // The seed for the world generator
    public static int seed;

    // The name this world is (to be) saved as
    public static string name = "world";

    public override byte[] section_id_bytes()
    {
        // There is only one world section, and it is always the same
        return new byte[] { };
    }

    public override void invert_id(byte[] id_bytes)
    {
        // Nothing needs doing. there is only one world
    }

    public override void section_id_initialize(params object[] section_id_init_args)
    {
        // No initialization neccassary
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
        if (seed == 0)
            throw new System.Exception("Invalid seed!");

        // Create the local player
        create<player>(player.username, true);
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