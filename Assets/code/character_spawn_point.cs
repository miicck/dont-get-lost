using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A point at which a character can spawn, in order
/// to fill the global quota of characters. </summary>
public class character_spawn_point : world_object.sub_generator
{
    public character to_spawn;

    bool active
    {
        get => active_spawn_points != null && active_spawn_points.Contains(this);
        set
        {
            // Remove from previous set
            if (value) inactive_spawn_points.Remove(this);
            else active_spawn_points.Remove(this);

            // Add to new set
            if (value) active_spawn_points.Add(this);
            else inactive_spawn_points.Add(this);
        }
    }

    public override void generate(biome.point point, chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        InvokeRepeating("enable_disable", 1, 1);
    }

    void enable_disable()
    {
        // Active if active in hierarchy and in range
        if (gameObject.activeInHierarchy)
            active = (transform.position - player.current.transform.position).magnitude < game.render_range;
        else active = false;
    }

    void OnDestroy()
    {
        // Remove from all spawner sets
        active_spawn_points.Remove(this);
        inactive_spawn_points.Remove(this);
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

    static HashSet<character_spawn_point> active_spawn_points;
    static HashSet<character_spawn_point> inactive_spawn_points;

    public static int active_count => active_spawn_points.Count;
    public static int inactive_count => inactive_spawn_points.Count;

    public static void initialize()
    {
        active_spawn_points = new HashSet<character_spawn_point>();
        inactive_spawn_points = new HashSet<character_spawn_point>();
    }

    /// <summary> Spawns a character in the world, returns true if successful. </summary>
    public static bool spawn()
    {
        if (active_spawn_points.Count == 0) return false;

        // Spawn at a random spawn point
        int spawn_index = Random.Range(0, active_spawn_points.Count);
        foreach (var sp in active_spawn_points)
            if (--spawn_index <= 0)
            {
                sp.spawn_character();
                return true;
            }

        return false;
    }
}