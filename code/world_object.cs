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

    // Spawn the world object with the given name at the given location
    public static world_object spawn(string name, chunk.location location)
    {
        // Look up the selected world object
        var wo = look_up(name);

        // Check it can be placed here
        if (location.altitude < wo.min_altitude) return null;
        if (location.altitude > wo.max_altitude) return null;
        if (location.terrain_angle > wo.max_terrain_angle) return null;
        if (location.fertility < wo.min_fertility) return null;

        // Create a copy of the world_object
        wo = wo.inst();

        wo.transform.SetParent(location.chunk.transform);
        wo.transform.position = location.world_position;

        if (wo.align_with_terrain_normal) wo.transform.up = location.terrain_normal;
        wo.transform.localScale = Vector3.one * Random.Range(wo.min_scale, wo.max_scale);
        if (wo.random_y_rotation) wo.transform.Rotate(0, Random.Range(0f, 360f), 0);

        return wo;
    }
}