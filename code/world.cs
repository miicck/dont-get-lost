using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public class world : networked
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    public networked_variable.net_int networked_seed;
    public networked_variable.net_string networked_name; 

    public override float network_radius()
    {
        // The world is always loaded
        return float.PositiveInfinity;
    }

    public override void on_init_network_variables()
    {
        networked_seed = new networked_variable.net_int();
        networked_name = new networked_variable.net_string();
        static_world = this;
        Invoke("start_generation", 0.1f);
    }

    private void start_generation()
    {
        if (player.current == null)
        {
            // Wait until the player is created before
            // generating the map (we need to know where
            // the player is to work out which bits of the
            // map to generate).
            Invoke("start_generation", 0.1f);
            return;
        }

        // We can start to generating the world
        var biome_coords = biome.coords(player.current.transform.position);
        biome.generate(biome_coords[0], biome_coords[1]);
    }

    static world static_world;

    // The seed for the world generator
    public static int seed
    {
        get => static_world.networked_seed.value;
    }

    // The name of the world
    public static string name
    {
        get => static_world.networked_name.value;
    }

    public static string info()
    {
        if (static_world == null) return "No world";
        return "World " + name + " (seed " + seed + ")";
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(world))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            world w = (world)target;
            UnityEditor.EditorGUILayout.IntField("static seed", seed);
            UnityEditor.EditorGUILayout.IntField("network seed", w.networked_seed.value);
        }
    }
#endif
}