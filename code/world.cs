using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public class world : networked
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    networked_variable.net_int networked_seed;

    public override float network_radius()
    {
        // The world is always loaded
        return float.PositiveInfinity;
    }

    public override void on_init_network_variables()
    {
        networked_seed = new networked_variable.net_int(seed);
        static_world = this;
    }

    static world static_world;

    // The seed for the world generator
    public static int seed
    {
        get => static_world.networked_seed.value;
        set => static_world.networked_seed.value = value;
    }

    // The name this world is (to be) saved as
    public static string name;

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