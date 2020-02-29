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
    public float min_fertility = 0f;

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
}