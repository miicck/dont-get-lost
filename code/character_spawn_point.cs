using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A point at which a character can spawn, in order
/// to fill the global quota of characters. </summary>
public class character_spawn_point : world_object.sub_generator
{
    public character to_spawn;

    bool active = false;

    public override void generate(biome.point point, chunk chunk,
        int x_in_chunk, int z_in_chunk)
    {
        chunk.add_generation_listener(chunk.x, chunk.z, (c) =>
        {
            active = true;
            spawn_points.Add(this);
        });
    }

    void OnDestroy()
    {
        active = false;
        spawn_points.Remove(this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = active ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    void spawn_character()
    {
        client.create(transform.position, "characters/" + to_spawn.name);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static List<character_spawn_point> spawn_points;

    public static void initialize()
    {
        spawn_points = new List<character_spawn_point>();
    }

    public static bool spawn()
    {
        if (spawn_points.Count == 0) return false;
        var sp = spawn_points[Random.Range(0, spawn_points.Count)];
        sp.spawn_character();
        return true;
    }
}