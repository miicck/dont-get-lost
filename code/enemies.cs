using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> This class controls the spawning of enemies that
/// aren't spawned by a <see cref="character_spawner"/>. </summary>
public static class enemies
{
    public const float TIME_BETWEEN_SPAWNS = 5f;

    public static bool enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!_enabled)
            {
                foreach (var c in spawend)
                    Object.Destroy(c);
                spawend.Clear();
            }
        }
    }
    static bool _enabled = true;

    static HashSet<character> spawend = new HashSet<character>();
    static Dictionary<string, float> character_densities;
    static float total_density = 0;
    static int target_count => Mathf.FloorToInt(total_density * Mathf.PI * game.render_range * game.render_range);
    static path spawn_path = null;
    static float next_spawn_time = 0;
    static float max_spawn_range => game.render_range * 0.75f;
    static float target_spawn_range => game.render_range / 2f;
    static float min_spawn_range => game.render_range / 4f;
    static character next_spawn;

    /// <summary> The biome controlling character generation. </summary>
    public static biome biome
    {
        get => _biome;
        set
        {
            if (_biome == value)
                return;

            // Update the biome info
            _biome = value;
            character_densities = value?.character_densities();

            // Work out how many characters we are aiming for
            total_density = 0;
            if (character_densities != null)
                foreach (var kv in character_densities)
                    total_density += kv.Value;

            // Get the first character to spawn
            generate_next_spawn();
        }
    }
    static biome _biome;

    public static void update()
    {
        // Remove deleted characters
        spawend.RemoveWhere((c) => c == null);

        // Keep the number of spawned characters up
        if (enabled)
            spawn_more();
    }

    public static void register_character(character c)
    {
        if (c.controller is default_character_control)
            spawend.Add(c);
    }

    /// <summary> Choose the next character to spawn, weighted by target density. </summary>
    static void generate_next_spawn()
    {
        next_spawn = null;
        if (character_densities == null)
            return;

        float total = 0;
        foreach (var kv in character_densities)
            total += kv.Value;

        float rnd = Random.Range(0, total);
        total = 0;
        foreach (var kv in character_densities)
        {
            total += kv.Value;
            if (total > rnd)
            {
                next_spawn = Resources.Load<character>("characters/" + kv.Key);
                return;
            }
        }
    }

    /// <summary> Pathing agent used to test for spawning. </summary>
    class spawn_test_agent : IPathingAgent
    {
        public Vector3 validate_position(Vector3 v, out bool valid)
        {
            if (v.y < world.SEA_LEVEL)
            {
                valid = false;
                return v;
            }
            return pathfinding_utils.validate_walking_position(v, resolution, out valid);
        }

        public bool validate_move(Vector3 a, Vector3 b)
        {
            return pathfinding_utils.validate_walking_move(a, b,
                resolution, 2f, resolution / 2f);
        }

        public float resolution { get => 1f; }
    }

    static void spawn_more()
    {
        if (player.current == null) return;
        if (spawend.Count >= target_count) return;
        if (next_spawn_time > Time.realtimeSinceStartup) return;

        // Create a random path for the next_spawn character, starting at the player
        // with a length between min_spawn_range and target_spawn_range
        if (spawn_path == null)
        {
            generate_next_spawn();
            spawn_path = new random_path(player.current.transform.position,
                (v) => (v - player.current.transform.position).magnitude > target_spawn_range,
                (v) => (v - player.current.transform.position).magnitude > min_spawn_range, new spawn_test_agent());
        }

        else switch (spawn_path.state)
            {
                case path.STATE.SEARCHING:
                    // Keep pathfinding
                    spawn_path.pathfind(load_balancing.iter);
                    break;

                case path.STATE.FAILED:
                    // Try again
                    spawn_path = null;
                    break;

                case path.STATE.COMPLETE:
                    // Found a suitable spawn location
                    Vector3 location = spawn_path[spawn_path.length - 1];
                    float dis = (player.current.transform.position - location).magnitude;

                    // Check that it is within range
                    if (dis > min_spawn_range && dis < max_spawn_range)
                    {
                        var spawn = (character)client.create(
                            spawn_path[spawn_path.length - 1], "characters/" + next_spawn.name);
                        spawend.Add(spawn);
                    }

                    // Move to next spawn
                    next_spawn_time = Time.realtimeSinceStartup + TIME_BETWEEN_SPAWNS;
                    spawn_path = null;
                    break;

                default:
                    throw new System.Exception("Unkown path state!");
            }
    }

    public static void draw_gizmos()
    {
        spawn_path?.draw_gizmos();
    }

    public static string info()
    {
        float next_spawn = Mathf.Max(0, next_spawn_time - Time.realtimeSinceStartup);
        return "    Enemies spawned : " + spawend.Count + "/" + target_count + "\n" +
               "    Next spawn in   : " + next_spawn + " s\n";
    }
}
