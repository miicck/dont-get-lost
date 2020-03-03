using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object : MonoBehaviour
{
    // Variables specifying how this world object is placed
    public bool align_with_terrain_normal = false;
    public bool random_y_rotation = true;
    public float min_scale = 0.5f;
    public float max_scale = 2.0f;
    public float min_altitude = world.SEA_LEVEL;
    public float max_altitude = world.MAX_ALTITUDE;
    public float max_terrain_angle = 90f;

    // The library of all loaded world_objects
    static Dictionary<string, world_object> library =
        new Dictionary<string, world_object>();
    public static world_object look_up(string name)
    {
        world_object ret = null;
        if (!library.TryGetValue(name, out ret))
        {
            ret = Resources.Load<world_object>("world_objects/" + name);
            library[name] = ret;
        }
        return ret;
    }

    public bool can_place(biome.point p, Vector3 terrain_normal)
    {
        if (p.altitude > max_altitude) return false;
        if (p.altitude < min_altitude) return false;
        if (Vector3.Angle(terrain_normal, Vector3.up) > max_terrain_angle) return false;
        return true;
    }

    public void on_placement(Vector3 terrain_normal)
    {
        transform.localScale = Vector3.one * Random.Range(min_scale, max_scale);
        if (random_y_rotation) transform.Rotate(0, Random.Range(0, 360f), 0);
        if (align_with_terrain_normal) transform.up = terrain_normal;
    }
}