using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Networked world information
public class world : networked, INotSavedInStartupFile
{
    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;
    public const float UNDERGROUND_ROOF = -2000f; // The level below which "underground" systems generate

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

    private void Update()
    {
        if (geometry_update_regions.Count > 0)
        {
            // Process geometry updates
            foreach (var f in geometry_change_listeners)
                f(geometry_update_regions);

            // Clear set of regiosn to update
            geometry_update_regions.Clear();
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static world static_world;

    // The seed for the world generator
    public static int seed => static_world.networked_seed.value;

    // The name of the world
    new public static string name => static_world.networked_name.value;

    public static float terrain_altitude(Vector3 v)
    {
        Vector3 start = v;
        start.y = 2 * MAX_ALTITUDE;
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(start, Vector3.down), out RaycastHit hit);
        if (tc == null) return SEA_LEVEL;
        return hit.point.y;
    }

    public delegate void geometry_change_listener(List<Bounds> modified_regions);
    static List<Bounds> geometry_update_regions = new List<Bounds>();
    static List<geometry_change_listener> geometry_change_listeners = new List<geometry_change_listener>();
    public static void on_geometry_change(Bounds region_affected) => geometry_update_regions.Add(region_affected);
    public static void add_geometry_change_listener(geometry_change_listener listener) => geometry_change_listeners.Add(listener);

    public static string info()
    {
        if (static_world == null) return "No world";
        return "World " + name + "\n" +
               "    Seed              : " + seed + "\n" +
               "    Biome             : " + player.current.biome.name + " (" + biome.active_biomes + " biomes active)\n" +
               "    Render range      : " + game.render_range + "\n" +
               "    Fog distance      : " + lighting.fog_distance + "\n" +
               "    Generated chunks  : " + chunk.chunks_generated + "\n" +
               "    Generating chunks : " + chunk.chunks_generating + " (" +
                    chunk.enabled_and_generating + " enabled, limit render range at " +
                    chunk.generating_limit + ")\n" +
               "    Save location     : " + server.local_save_file();
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(world))]
    new class editor : networked.editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            world w = (world)target;
            if (!Application.isPlaying) return;
            UnityEditor.EditorGUILayout.IntField("static seed", seed);
            UnityEditor.EditorGUILayout.IntField("network seed", w.networked_seed.value);
        }
    }
#endif
}