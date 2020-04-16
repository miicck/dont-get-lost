using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class character : MonoBehaviour
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

    void get_path(Vector3 target)
    {
        path = new path(transform.position, target, min_altitude: can_swim ? 0 : world.SEA_LEVEL);
    }

    // The last position that we checked which chunk we were in
    Vector3 last_chunk_check_position;
    chunk _chunk;
    public chunk chunk
    {
        get { return _chunk; }
        private set
        {
            if (_chunk == value) return;
            if (value == null) return;
            _chunk = value;
            transform.SetParent(_chunk.transform);
        }
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
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Random.onUnitSphere * 5f, -Vector3.up, out hit, 10f))
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

    void update_chunk(bool forced = false)
    {
        // No updates needed if we haven't moved by more than 1m in the x,y plane from last update
        Vector3 delta = transform.position - last_chunk_check_position; delta.y = 0;
        if (delta.magnitude < 1f && !forced) return;
        last_chunk_check_position = transform.position;
        chunk = chunk.at(transform.position);
    }

    private void Start()
    {
        // Force a chunk update
        update_chunk(true);
    }

    private void Update()
    {
        if ((transform.position - player.current.transform.position).magnitude < 5f)
            flee(player.current.transform);
        else
            idle_walk();
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
