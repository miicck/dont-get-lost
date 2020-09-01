using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Networked world information
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

    public override void on_init_network_variables()
    {
        networked_seed = new networked_variables.net_int();
        networked_name = new networked_variables.net_string();
        static_world = this;
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
               "    Render range : " + game.render_range + "\n" +
               "    Fog distance : " + lighting.fog_distance;
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