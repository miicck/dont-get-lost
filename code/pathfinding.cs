//#define PATH_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class path
{
    // Variables specifying how to
    // carry out the pathfinding
    Vector3 start;
    Vector3 goal;
    float agent_height;
    float resolution;
    float max_incline;
    float ground_clearance;
    constraint_func constraint;
    int max_iterations;
    Transform ignore_collisions_with;

    // Pathfinding state variables
    int iteration_count = 0;
    waypoint start_waypoint;
    waypoint goal_waypoint;
    waypoint current;
    List<waypoint> path_found;
    Dictionary<int, int, int, waypoint> open_set = new Dictionary<int, int, int, waypoint>();
    Dictionary<int, int, int, waypoint> closed_set = new Dictionary<int, int, int, waypoint>();

    /// <summary> Get the i^th vector in the path. </summary>
    public Vector3 this[int i]
    {
        get
        {
            if (i == 0) return start;
            if (i - 1 < path_found.Count)
                return path_found[i - 1].grounding;
            return goal;
        }
    }

    /// <summary> Return the number of vectors in the path.
    /// This is the waypoints + the start and the end. </summary>
    public int length
    {
        get
        {
            if (path_found == null) return 0;
            return path_found.Count + 2;
        }
    }

    /// <summary> Describes a stage of pathfinding. </summary>
    public enum STATE
    {
        FAILED,
        SEARCHING,
        COMPLETE
    }

    /// <summary> The stage of pathfinding we're at. </summary>
    public STATE state
    {
        get => _state;
        private set
        {
            _state = value;
            if (value == STATE.FAILED && failure_reason == FAILURE_REASON.NONE)
                throw new System.Exception("Failed without a reason!");

            if (value != STATE.SEARCHING)
            {
#               if PATH_DEBUG
                // Don't free memory immediately in debug mode
#               else
                // Free memory used by pathfinding stuff
                open_set = null;
                closed_set = null;
                current = null;
                start_waypoint = null;
                goal_waypoint = null;
#               endif
            }
        }
    }
    STATE _state;

    public enum FAILURE_REASON
    {
        NONE,
        NO_PATH,
        MAX_ITER_HIT,
        INVALID_START,
        INVALID_END,
    }
    public FAILURE_REASON failure_reason { get; private set; }

    /// <summary> An intermediate point along the path. </summary>
    class waypoint
    {
        public int x;
        public int y;
        public int z;
        public bool open;
        public Vector3 grounding;
        public float fscore = float.PositiveInfinity;
        public float gscore = float.PositiveInfinity;
        public waypoint came_from;
    }

    /// <summary> Find a grounding point within the 
    /// waypoint box with coordinates x, y, z. </summary>
    bool find_grounding(int x, int y, int z, out RaycastHit grounding)
    {
        Vector3 top = new Vector3(x, y + 0.5f, z) * resolution + start;

        // Raycast from each of these points on the top of the box
        // downward to the bottom of the box to find a grounding point
        Vector3[] cast_starts = new Vector3[]
        {
            top,
            top + new Vector3(0.5f, 0, 0.5f) * resolution,
            top + new Vector3(-0.5f, 0, 0.5f) * resolution,
            top + new Vector3(0.5f, 0, -0.5f) * resolution,
            top + new Vector3(-0.5f, 0, -0.5f) * resolution
        };

        foreach (var v in cast_starts)
            foreach (var hit in Physics.RaycastAll(v, Vector3.down, resolution))
                if (!hit.transform.IsChildOf(ignore_collisions_with))
                {
                    grounding = hit;
                    return true;
                }

        grounding = default;
        return false;
    }

    /// <summary> Returns true if we can move between the given
    /// waypoints without hitting any geometry. </summary>
    bool can_move_between(waypoint a, waypoint b)
    {
        // Apply the ground clearance
        Vector3 ag = a.grounding + Vector3.up * ground_clearance;
        Vector3 bg = b.grounding + Vector3.up * ground_clearance;
        Vector3 delta = bg - ag;

        Vector3 box_size = new Vector3(
            0.1f,
            agent_height - ground_clearance,
            0.1f);

        Vector3 start = ag + (ground_clearance + agent_height * 0.5f) * Vector3.up;

        foreach (var hit in Physics.BoxCastAll(
            start,                // Start of box cast
            box_size * 0.5f,      // Half-extent of box to cast
            delta.normalized,     // Direction to cast
            Quaternion.identity,  // Rotation of box to cast
            delta.magnitude       // Distance to cast
            ))
        {
            if (!hit.transform.IsChildOf(ignore_collisions_with))
                return false;
        }

        return true;
    }

    /// <summary> Load a waypoint from the open/closed set. If not 
    /// found, attempt to create a new waypoint. The new waypoint
    /// will be added to the open set if it is pathable or the closed 
    /// set if it is not pathable. </summary>
    waypoint load(int x, int y, int z, waypoint parent = null)
    {
        // Attempt to load from the open set
        var found = open_set.get(x, y, z);
        if (found != null)
        {
            found.open = true;
            return found;
        }

        // Attempt to load from the closed set
        found = closed_set.get(x, y, z);
        if (found != null)
        {
            found.open = false;
            return found;
        }

        // Create a new waypoint
        var wp = new waypoint
        {
            x = x,
            y = y,
            z = z
        };

        // This is the centre of the waypoint box
        Vector3 centre = start + new Vector3(x, y, z) * resolution;

        // Attempt to find grounding within the waypoint box
        if (!find_grounding(x, y, z, out RaycastHit hit))
        {
            // Waypoint doesn't have grounding, add to closed set
            wp.open = false;
            closed_set.set(x, y, z, wp);
            return wp;
        }

        // Record the grounding point
        wp.grounding = hit.point;

        // Check the grounding point satisfies the constraint
        if (!constraint(wp.grounding))
        {
            wp.open = false;
            closed_set.set(x, y, z, wp);
            return wp;
        }

        float angle = Vector3.Angle(Vector3.up, hit.normal);
        if (angle > max_incline)
        {
            // Ground is too steep here, add to the closed set
            wp.open = false;
            closed_set.set(x, y, z, wp);
            return wp;
        }

        // Check that we can access this waypoint from the parent
        if (parent != null && !can_move_between(parent, wp))
        {
            // Reject ths waypoint, but don't add it to the 
            // closed set as we might be able to access it
            // from another direction.
            return null;
        }

        // This waypoint is fine, add to the open set
        wp.open = true;
        open_set.set(x, y, z, wp);
        return wp;
    }

    /// <summary> Overlaod of <see cref="load(int, int, int)"/> 
    /// for a position, rather than coordinates. Searches
    /// outward from the grid point closest to v until a 
    /// valid grid point is found. </summary>
    waypoint load(Vector3 v)
    {
        // Find the nearest grounding point to v in the up/down direction
        Vector3? grounding = null;
        float min_dis = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(v + Vector3.up * 10, Vector3.down, 20))
            if (!hit.transform.IsChildOf(ignore_collisions_with))
            {
                float dis = (hit.point - v).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    grounding = hit.point;
                }
            }

        if (grounding != null)
            v = (Vector3)grounding;

        Vector3 d = v - start;
        int x0 = Mathf.RoundToInt(d.x / resolution);
        int y0 = Mathf.RoundToInt(d.y / resolution);
        int z0 = Mathf.RoundToInt(d.z / resolution);

        Vector3 distance = goal - start;
        int max = (int)(distance.magnitude / resolution);
        if (max < 2) max = 2;

        // Find the nearest valid waypoint
        waypoint found = null;
        utils.search_outward(x0, y0, z0, max, (x, y, z) =>
        {
            found = load(x, y, z);
            return found.open;
        });

        return found;
    }

    /// <summary> Heuristic used to guide pathfinding. </summary>
    float heuristic(waypoint a, waypoint b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
    }

    public delegate bool constraint_func(Vector3 v);

    public path(
        Vector3 start,
        Vector3 goal,
        Transform ignore_collisions_with,
        float agent_height,
        float resolution,
        float max_incline = 60f,
        float ground_clearance = -1,
        constraint_func constraint = null,
        int max_iterations = 1000)
    {
        // Default: work out ground clearance as
        // fraction of agent height
        if (ground_clearance < 0)
            ground_clearance = agent_height / 4f;

        // null constraint => no constraint
        if (constraint == null)
            constraint = (v) => true;

        this.start = start;
        this.goal = goal;
        this.agent_height = agent_height;
        this.resolution = resolution;
        this.max_incline = max_incline;
        this.ground_clearance = ground_clearance;
        this.constraint = constraint;
        this.max_iterations = max_iterations;
        this.ignore_collisions_with = ignore_collisions_with;

        start_waypoint = load(start);
        if (!start_waypoint.open)
        {
            // Start point was invalid
            failure_reason = FAILURE_REASON.INVALID_START;
            state = STATE.FAILED;
            return;
        }

        goal_waypoint = load(goal);
        if (!goal_waypoint.open)
        {
            // Goal point was invalid
            failure_reason = FAILURE_REASON.INVALID_END;
            state = STATE.FAILED;
            return;
        }

        start_waypoint.gscore = 0;
        start_waypoint.fscore = heuristic(start_waypoint, goal_waypoint);
        state = STATE.SEARCHING;
    }

    /// <summary> Run <paramref name="iterations"/> pathfinding 
    /// iterations. </summary>
    public void pathfind(int iterations)
    {
        if (state != STATE.SEARCHING)
            return; // Not searching

        for (int i = 0; i < iterations; ++i)
        {
            if (++iteration_count >= max_iterations)
            {
                // Max iterations hit
                failure_reason = FAILURE_REASON.MAX_ITER_HIT;
                state = STATE.FAILED;
                return;
            }

            // Find the waypoint in the open set with the lowest f-score
            float min_fscore = float.PositiveInfinity;
            current = null;
            open_set.iterate((x, y, z, w) =>
            {
                if (w.fscore < min_fscore || current == null)
                {
                    current = w;
                    min_fscore = w.fscore;
                }
            });

            // Check for success
            if (current == goal_waypoint)
            {
                if (current.came_from == null)
                {
                    // We haven't actually found a way to
                    // the target, it was simply the only
                    // remaining waypoint in the open set.
                    failure_reason = FAILURE_REASON.NO_PATH;
                    state = STATE.FAILED;
                    return;
                }

                // Reconstruct the path (backwards)
                path_found = new List<waypoint>();
                while (current.came_from != null)
                {
                    path_found.Add(current.came_from);
                    current = current.came_from;
                }
                path_found.Reverse();
                state = STATE.COMPLETE;
                return;
            }

            if (current == null)
            {
                // Open set was empty => pathfinding failed
                failure_reason = FAILURE_REASON.NO_PATH;
                state = STATE.FAILED;
                return;
            }

            if (current.fscore > float.MaxValue)
                throw new System.Exception("Infinite score encountered!");

            // Remove current from the open set, add to the closed set
            open_set.clear(current.x, current.y, current.z);
            closed_set.set(current.x, current.y, current.z, current);

            // Loop over neighbours of current
            for (int n = 0; n < utils.neighbouring_dxs_3d.Length; ++n)
            {
                int dx = utils.neighbouring_dxs_3d[n];
                int dy = utils.neighbouring_dys_3d[n];
                int dz = utils.neighbouring_dzs_3d[n];

                var neighbour = load(current.x + dx, current.y + dy, current.z + dz, parent: current);
                if (neighbour == null) continue; // This neihbour was inaccessable
                float tgs = current.gscore + 1;

                if (tgs < neighbour.gscore)
                {
                    // This is a shorter path to neighbour, record it
                    neighbour.gscore = tgs;
                    neighbour.fscore = tgs + heuristic(neighbour, goal_waypoint);
                    neighbour.came_from = current;
                }
            }
        }
    }

    /// <summary> Returns information about the 
    /// setup of this path instance. </summary>
    public string info()
    {
        string fail_string = "";
        if (failure_reason != FAILURE_REASON.NONE)
            fail_string = "Failure mode " + failure_reason + "\n";

        return
        "Status " + state + "\n" + fail_string +
        "Start " + start + "\n" +
        "Goal " + goal + "\n" +
        "Agent height " + agent_height + "\n" +
        "Resoulution " + resolution + "\n" +
        "Max incline " + max_incline + "\n" +
        "Ground clearance " + ground_clearance;
    }

    public void draw_gizmos()
    {
        // Draw the start and end points
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(start, 0.2f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(goal, 0.2f);

        // This function will be called on all waypoints
        Dictionary<int, int, int, waypoint>.iter_func draw = (x, y, z, w) =>
        {
            if (w.fscore > float.MaxValue)
            {
                // This is an infinite score waypoint
                // highlight it with a cube (this should
                // only be allowed to happen at the goal
                // point).
                Gizmos.DrawWireCube(
                    new Vector3(x, y, z) * resolution + start,
                    Vector3.one * resolution);
                return;
            }

            // If this waypoint didn't come from anywhere,
            // don't draw the line leading to it
            waypoint came_from = w.came_from;
            if (came_from == null)
                return;

            // If this waypoint does't have a grounding point,
            // don't draw the line leading to it
            if (w.grounding == default || came_from.grounding == default)
                return;

            // Draw line from parent to this waypoint
            Gizmos.DrawLine(w.grounding, came_from.grounding);
        };

        // Closed waypoints are dark blue
        Gizmos.color = Color.blue;
        closed_set?.iterate(draw);

        // Open waypoints are cyan
        Gizmos.color = Color.cyan;
        open_set?.iterate(draw);

        // The current waypoint is highlighted red
        if (current != null)
        {
            Gizmos.color = Color.red;
            draw(current.x, current.y, current.z, current);
        }

        // Draw the path 
        Gizmos.color = Color.green;
        for (int i = 1; i < length; ++i)
            Gizmos.DrawLine(this[i - 1], this[i]);
    }
}