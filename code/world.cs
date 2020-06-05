using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public class world : networked
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    public networked_variables.net_int networked_seed;
    public networked_variables.net_string networked_name;

    public override float network_radius()
    {
        // The world is always loaded
        return float.PositiveInfinity;
    }

    bool generation_requested = false;

    public override void on_init_network_variables()
    {
        networked_seed = new networked_variables.net_int();
        networked_name = new networked_variables.net_string();
        static_world = this;
        generation_requested = true;
    }

    private void attempt_start_generation()
    {
        if (player.current == null)
        {
            // Wait until the player is created before
            // generating the map (we need to know where
            // the player is to work out which bits of the
            // map to generate).
            return;
        }

        // We can start to generating the world
        generation_requested = false;
        biome.initialize();
        var biome_coords = biome.coords(player.current.transform.position);
        biome.generate(biome_coords[0], biome_coords[1]);
    }

    private void Update()
    {
        if (generation_requested) attempt_start_generation();
    }

    static world static_world;

    // The seed for the world generator
    public static int seed
    {
        get => static_world.networked_seed.value;
    }

    // The name of the world
    new public static string name
    {
        get => static_world.networked_name.value;
    }

    public static string info()
    {
        if (static_world == null) return "No world";
        return "World " + name + "\n" +
               "    Seed         : " + seed + "\n" +
               "    Biome        : " + player.current.biome.GetType().Name + "\n" +
               "    Render range : " + game.render_range;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(world))]
    new class editor : UnityEditor.Editor
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