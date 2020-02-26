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

    // The library of world objects, indexed by type
    static world_object[] library = null;

    // Create a random world object.
    public static world_object create_random()
    {
        if (library == null)
            library = Resources.LoadAll<world_object>("world_objects/");
        return library[Random.Range(0, library.Length)].inst();
    }

    // Spawn the world object with the given name at the given location
    public static world_object spawn(string name, chunk.location location)
    {
        var wo = Resources.Load<world_object>("world_objects/" + name).inst();

        if (location.altitude < wo.min_altitude) goto destroy;
        if (location.altitude > wo.max_altitude) goto destroy;

        wo.transform.SetParent(location.chunk.transform);
        wo.transform.position = location.world_position;

        if (wo.align_with_terrain_normal) wo.transform.up = location.terrain_normal;
        wo.transform.localScale = Vector3.one * Random.Range(wo.min_scale, wo.max_scale);
        if (wo.random_y_rotation) wo.transform.Rotate(0, Random.Range(0f, 360f), 0);

        return wo;

    destroy:
        Destroy(wo.gameObject);
        return null;
    }
}