using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class character : networked
{
    // A character is considered to have arrived at a point
    // if they are within this distance of it.
    public const float ARRIVE_DISTANCE = 0.25f;

    // The speed of the character
    public float walk_speed = 1f;
    public float run_speed = 4f;

    // Can I walk on land, or swim in water
    public bool can_walk = true;
    public bool can_swim = false;

    // The current path the character is walking
    path _path;
    path path
    {
        get { return _path; }
        set { _path = value; path_progress = 0; }
    }
    int path_progress = 0;

    // The character spawner that spawned me
    character_spawner spawned_by
    {
        get
        {
            if (_spawned_by == null)
            {
                _spawned_by = transform.parent.GetComponent<character_spawner>();
                if (_spawned_by == null)
                    throw new System.Exception("Character parent is not a character spawner!");
            }
            return _spawned_by;
        }
    }
    character_spawner _spawned_by;

    void get_path(Vector3 target)
    {
        path = new path(transform.position, target, constraint: (v) =>
        {
            // Constraints on path

            // Check we're in the right medium
            if (!can_swim && v.y < world.SEA_LEVEL) return false;
            if (!can_walk && v.y > world.SEA_LEVEL) return false;

            // Can't get too far from spawner
            if ((v - spawned_by.transform.position).magnitude > spawned_by.max_range) return false;

            return true;
        });
    }

    void move_along_path(float speed)
    {
        if (!path.complete)
        {
            // Run pathfinding
            path.run_pathfinding(1);
            return;
        }
        else
        {
            if (path.length <= path_progress)
            {
                // Path complete, reset
                path = null;
                return;
            }

            // Work out how far to the next path point
            Vector3 delta = path[path_progress] - transform.position;
            if (delta.magnitude < ARRIVE_DISTANCE) ++path_progress;
            transform.position += delta.normalized * speed * Time.deltaTime;

            // Look in the direction of travel
            delta.y = 0;
            if (delta.magnitude > 10e-4)
                transform.forward = Vector3.Lerp(transform.forward,
                    delta.normalized, speed * 5f * Time.deltaTime);
        }
    }

    // Just idly wonder around
    void idle_walk()
    {
        if (path == null)
        {
            // Search for a new walk target
            Vector3 location = spawned_by.transform.position + 
                Random.insideUnitSphere * spawned_by.max_range;

            RaycastHit hit;
            if (Physics.Raycast(location, -Vector3.up, out hit, 10f))
                get_path(hit.point);
        }
        else move_along_path(walk_speed);
    }

    // Run from the given transform
    void flee(Transform fleeing_from)
    {
        if (path == null)
        {
            Vector3 delta = transform.position - fleeing_from.position;
            get_path(transform.position + delta.normalized * 5f);
        }
        else move_along_path(run_speed);
    }

    private void Update()
    {
        if (!has_authority) return;

        if ((transform.position - player.current.transform.position).magnitude < 5f)
            flee(player.current.transform);
        else
            idle_walk();
    }

    public override void on_network_update()
    {
        if (has_authority)
            networked_position = transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw path gizmos
        if (path != null)
            path.draw_gizmos();
    }

    // The characters in the resources/characters directory
    static Dictionary<string, character> character_library;
    public static character load(string name)
    {
        if (character_library == null)
        {
            character_library = new Dictionary<string, character>();
            foreach (var c in Resources.LoadAll<character>("characters/"))
                character_library[c.name] = c;
        }
        return character_library[name];
    }
}
