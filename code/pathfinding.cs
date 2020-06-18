//#define PATH_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

            if (path_found.Count > 0)
            {
                var last = path_found[path_found.Count - 1].grounding;
                Vector3 delta = goal - last;
                if (delta.magnitude < resolution) return goal;
                return last;
            }
            else
            {
                Vector3 delta = goal - start;
                if (delta.magnitude < resolution) return goal;
                return start;
            }
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

public static class pathfinding_utils
{
    public static Vector3 boxcast_position_validate(Vector3 centre, float resolution, out bool valid)
    {
        Vector3 size = Vector3.one * resolution;
        Vector3 start_pos = centre + Vector3.up * resolution;
        Vector3 end_pos = centre;

        Vector3 move = end_pos - start_pos;
        valid = Physics.BoxCast(start_pos, size / 2f, move.normalized,
            out RaycastHit hit, Quaternion.identity, move.magnitude);

        if (!valid) return centre;
        return hit.point;
    }

    public static bool boxcast_move_validate(Vector3 a, Vector3 b, float resolution, float ground_clearance)
    {
        Vector3 size = Vector3.one * resolution;
        Vector3 local_centre = (size.y + ground_clearance / 2f) * Vector3.up / 2f;
        size.y -= ground_clearance;
        Vector3 start = a + local_centre;
        Vector3 end = b + local_centre;
        Vector3 move = end - start;

        return !Physics.BoxCast(start, size / 2f, move.normalized,
            out RaycastHit hit, Quaternion.identity, move.magnitude);
    }

    public static bool linecast_move_validate(Vector3 a, Vector3 b, float ground_clearance)
    {
        Vector3 start = a + ground_clearance * Vector3.up;
        Vector3 end = b + ground_clearance * Vector3.up;
        Vector3 delta = end - start;
        return !Physics.Raycast(start, delta.normalized, delta.magnitude);
    }

    public static bool capsulecast_move_validate(Vector3 a, Vector3 b,
        float width, float height, float ground_clearance)
    {
        float eff_height = height - ground_clearance;
        if (width > eff_height)
            throw new System.Exception("Width > height in capsule!");

        float radius = width / 2f;

        Vector3 start_p1 = a + (radius + ground_clearance) * Vector3.up;
        Vector3 start_p2 = a + (eff_height - radius) * Vector3.up;
        Vector3 move = b - a;

        return !Physics.CapsuleCast(start_p1, start_p2, radius, move, move.magnitude);
    }

    public static bool capsulecast_move_validate_with_grounding(Vector3 a, Vector3 b,
    float width, float height, float ground_clearance)
    {
        if (!capsulecast_move_validate(a, b, width, height, ground_clearance)) return false;

        Vector3 delta = b - a;
        for (float p = 0; p <= delta.magnitude; p += width)
        {
            Vector3 middle = a + delta.normalized * p;
            Vector3 start = middle + Vector3.up * ground_clearance;
            Vector3 end = middle - Vector3.up * ground_clearance;
            Vector3 delta_ray = end - start;
            if (!Physics.Raycast(start, delta_ray.normalized, delta_ray.magnitude))
                return false;
        }

        return true;
    }
}

public abstract class generic_path
{
    public abstract void pathfind(int iterations);

    public abstract Vector3 this[int i] { get; }
    public abstract int length { get; }

    public enum STATE
    {
        SEARCHING,
        FAILED,
        COMPLETE
    }
    public STATE state { get; protected set; }

    public virtual void draw_gizmos() { }
    public virtual string info_text() { return "Path of type " + GetType().Name; }

    public delegate Vector3 position_validator(Vector3 v, out bool valid);
    protected position_validator validate_position;

    public delegate bool move_validator(Vector3 a, Vector3 b);
    protected move_validator validate_move;

    protected Vector3 start;
    protected Vector3 goal;

    public generic_path(Vector3 start, Vector3 goal,
        position_validator validate_position, move_validator validate_move)
    {
        this.validate_position = validate_position;
        this.validate_move = validate_move;
        this.start = start;
        this.goal = goal;
    }
}

public class dict_path : generic_path
{
    // Variables specifying how to
    // carry out the pathfinding
    float resolution;
    int max_iterations;

    // Pathfinding state variables
    int iteration_count = 0;
    waypoint start_waypoint;
    waypoint goal_waypoint;
    waypoint current;
    List<waypoint> path_found;
    Dictionary<int, int, int, waypoint> open_set = new Dictionary<int, int, int, waypoint>();
    Dictionary<int, int, int, waypoint> closed_set = new Dictionary<int, int, int, waypoint>();
    Dictionary<int, int, int, waypoint> invalid_set = new Dictionary<int, int, int, waypoint>();

    /// <summary> Get the i^th vector in the path. </summary>
    public override Vector3 this[int i]
    {
        get
        {
            if (i == 0) return start;
            if (i - 1 < path_found.Count)
                return path_found[i - 1].grounding;

            if (path_found.Count > 0)
            {
                var last = path_found[path_found.Count - 1].grounding;
                Vector3 delta = goal - last;
                if (delta.magnitude < resolution) return goal;
                return last;
            }
            else
            {
                Vector3 delta = goal - start;
                if (delta.magnitude < resolution) return goal;
                return start;
            }
        }
    }

    /// <summary> Return the number of vectors in the path.
    /// This is the waypoints + the start and the end. </summary>
    public override int length
    {
        get
        {
            if (path_found == null) return 0;
            return path_found.Count + 2;
        }
    }

    /// <summary> The possible modes of failure of pathfinding. </summary>
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
        public enum TYPE
        {
            OPEN,
            CLOSED,
            INVALID
        }

        public int x;
        public int y;
        public int z;
        public TYPE type;
        public Vector3 grounding;
        public float fscore = float.PositiveInfinity;
        public float gscore = float.PositiveInfinity;
        public waypoint came_from;
    }

    /// <summary> Load a waypoint from the open/closed set. If not 
    /// found, attempt to create a new waypoint. The new waypoint
    /// will be added to the open set if it is pathable or the invalid 
    /// set if it is not pathable. If invalid or not accesible from
    /// the given parent, will return null. </summary>
    waypoint load(int x, int y, int z, waypoint parent = null)
    {
        // Attempt to load from the open set
        var found = open_set.get(x, y, z);
        if (found != null)
        {
            found.type = waypoint.TYPE.OPEN;
            return found;
        }

        // Attempt to load from the closed set
        found = closed_set.get(x, y, z);
        if (found != null)
        {
            found.type = waypoint.TYPE.CLOSED;
            return found;
        }

        found = invalid_set.get(x, y, z);
        if (found != null)
            return null;

        // Create a new waypoint
        var wp = new waypoint
        {
            x = x,
            y = y,
            z = z
        };

        // This is the centre of the waypoint box
        wp.grounding = start + new Vector3(x, y, z) * resolution;

        // Attempt to find grounding within the waypoint box
        wp.grounding = validate_position(wp.grounding, out bool valid);
        if (!valid)
        {
            // Waypoint position invalid, add to the invalid set
            wp.type = waypoint.TYPE.INVALID;
            invalid_set.set(x, y, z, wp);
            return wp;
        }

        // Check that we can access this waypoint from the parent
        if (parent != null && !validate_move(parent.grounding, wp.grounding))
        {
            // Reject ths waypoint, but don't add it to the 
            // closed set as we might be able to access it
            // from another direction.
            return null;
        }

        // This waypoint is fine, add to the open set
        wp.type = waypoint.TYPE.OPEN;
        open_set.set(x, y, z, wp);
        return wp;
    }

    /// <summary> Overlaod of <see cref="load(int, int, int)"/> 
    /// for a position, rather than coordinates. Searches
    /// outward from the grid point closest to v until a 
    /// valid grid point is found. </summary>
    waypoint load(Vector3 v)
    {
        Vector3 d = v - start;
        int x0 = Mathf.RoundToInt(d.x / resolution);
        int y0 = Mathf.RoundToInt(d.y / resolution);
        int z0 = Mathf.RoundToInt(d.z / resolution);

        Vector3 distance = goal - start;
        int max = (int)(distance.magnitude / resolution);
        if (max < 2) max = 2;

        // Find the nearest open waypoint
        waypoint found = null;
        utils.search_outward(x0, y0, z0, max, (x, y, z) =>
        {
            found = load(x, y, z);
            if (found == null) return false;
            return found.type == waypoint.TYPE.OPEN;
        });

        return found;
    }

    /// <summary> Heuristic used to guide pathfinding. </summary>
    float heuristic(waypoint a, waypoint b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
    }

    public dict_path(Vector3 start, Vector3 goal, float resolution, int max_iterations,
        position_validator validate_position, move_validator validate_move) :
        base(start, goal, validate_position, validate_move)
    {
        this.resolution = resolution;
        this.max_iterations = max_iterations;

        start_waypoint = load(start);
        if (start_waypoint.type != waypoint.TYPE.OPEN)
        {
            // Start point was invalid
            failure_reason = FAILURE_REASON.INVALID_START;
            state = STATE.FAILED;
            return;
        }

        goal_waypoint = load(goal);
        if (goal_waypoint.type != waypoint.TYPE.OPEN)
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
    public override void pathfind(int iterations)
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
            current.type = waypoint.TYPE.CLOSED;

            // Loop over neighbours of current
            for (int n = 0; n < utils.neighbouring_dxs_3d.Length; ++n)
            {
                int dx = utils.neighbouring_dxs_3d[n];
                int dy = utils.neighbouring_dys_3d[n];
                int dz = utils.neighbouring_dzs_3d[n];

                var neighbour = load(current.x + dx, current.y + dy, current.z + dz, parent: current);
                if (neighbour == null) continue; // Invalid, or inaccessable
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
    public override string info_text()
    {
        string fail_string = "";
        if (failure_reason != FAILURE_REASON.NONE)
            fail_string = "Failure mode " + failure_reason + "\n";

        return
        "Status " + state + "\n" + fail_string +
        "Start " + start + "\n" +
        "Goal " + goal + "\n" +
        "Open set size " + open_set.count + "\n" +
        "Closed set size " + closed_set.count + "\n";
    }

    public override void draw_gizmos()
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

public abstract class generic_grid_path : generic_path
{
    public generic_grid_path(Vector3 start, Vector3 goal, float resolution,
        position_validator validate_position, move_validator validate_move)
        : base(start, goal, validate_position, validate_move)
    {
        this.resolution = resolution;
    }

    protected float resolution;

    protected class waypoint
    {
        public int x { get; private set; }
        public int y { get; private set; }
        public int z { get; private set; }

        public int manhattan_heuristic(waypoint other)
        {
            return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y) + Mathf.Abs(z - other.z);
        }

        public waypoint(int x, int y, int z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public override bool Equals(object obj)
        {
            if (obj is waypoint)
            {
                var w = (waypoint)obj;
                return w.x == x && w.y == y && w.z == z;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int xh = x >= 0 ? 2 * x : 2 * (-x) - 1;
            int yh = y >= 0 ? 2 * y : 2 * (-y) - 1;
            int zh = z >= 0 ? 2 * z : 2 * (-z) - 1;
            int xyh = (xh + yh) * (xh + yh + 1) / 2 + yh;
            return (xyh + zh) * (xyh + xh + 1) / 2 + zh;
        }

        public delegate void iter_func(int x, int y, int z);
        public void iterate_neighbours(iter_func f)
        {
            for (int i = 0; i < utils.neighbouring_dxs_3d.Length; ++i)
                f(x + utils.neighbouring_dxs_3d[i],
                  y + utils.neighbouring_dys_3d[i],
                  z + utils.neighbouring_dzs_3d[i]);
        }
    }

    protected void coordinates(Vector3 pos, out int x, out int y, out int z)
    {
        x = Mathf.RoundToInt((pos - start).x / resolution);
        y = Mathf.RoundToInt((pos - start).y / resolution);
        z = Mathf.RoundToInt((pos - start).z / resolution);
    }

    protected Vector3 centre(int x, int y, int z)
    {
        return start + new Vector3(x, y, z) * resolution;
    }

    protected delegate T waypoint_builder<T>(int x, int y, int z, Vector3 v);

    protected T closest_valid<T>(Vector3 pos, waypoint_builder<T> builder) where T : waypoint
    {
        T ret = default;
        coordinates(pos, out int x0, out int y0, out int z0);
        utils.search_outward(x0, y0, z0, 32, (x, y, z) =>
        {
            Vector3 c = centre(x, y, z);
            c = validate_position(c, out bool valid);
            if (!valid) return false;
            ret = builder(x, y, z, c);
            return true;
        });
        return ret;
    }
}

public class astar_path : generic_grid_path
{
    new protected class waypoint : generic_grid_path.waypoint
    {
        public int heuristic;
        public int g_score = int.MaxValue;
        public int f_score { get => g_score + heuristic; }

        public waypoint came_from;
        public Vector3 point;

        public waypoint(int x, int y, int z) : base(x, y, z) { }
    }

    protected class waypoint_comp : IComparer<waypoint>
    {
        public int Compare(waypoint a, waypoint b) { return a.heuristic.CompareTo(b.heuristic); }
    }

    waypoint start_waypoint;
    waypoint goal_waypoint;
    SortedDictionary<waypoint, waypoint> open_set;
    HashSet<waypoint> closed_set;
    HashSet<waypoint> invalid_set;
    List<Vector3> path;

    public override int length => path.Count;
    public override Vector3 this[int i] => path[i];

    public astar_path(Vector3 start, Vector3 goal, float resolution, int max_iterations,
        position_validator validate_position, move_validator validate_move) :
        base(start, goal, resolution, validate_position, validate_move)
    {
        start_waypoint = closest_valid(start, (x, y, z, v) => new waypoint(x, y, z)
        {
            point = v,
            g_score = 0
        });

        goal_waypoint = closest_valid(goal, (x, y, z, v) => new waypoint(x, y, z)
        {
            point = v
        });

        start_waypoint.heuristic = start_waypoint.manhattan_heuristic(goal_waypoint);

        open_set = new SortedDictionary<waypoint, waypoint>(new waypoint_comp());
        open_set[start_waypoint] = start_waypoint;
        closed_set = new HashSet<waypoint>();
        invalid_set = new HashSet<waypoint>();
        state = STATE.SEARCHING;
    }

    public override void pathfind(int iterations)
    {
        for (int i = 0; i < iterations; ++i)
        {
            var current = open_set.First().Value;

            if (current.Equals(goal_waypoint))
            {
                path = new List<Vector3>();
                while (current.came_from != null)
                {
                    path.Add(current.point);
                    current = current.came_from;
                }
                path.Reverse();
                state = STATE.COMPLETE;
                return;
            }

            open_set.Remove(current);
            closed_set.Add(current);

            current.iterate_neighbours((x, y, z) =>
            {
                // Attempt to find neighbour if they alreaddy exist
                var n = new waypoint(x, y, z);

                // Neighbour already closed
                if (closed_set.Contains(n)) return;
                if (invalid_set.Contains(n)) return;

                if (open_set.TryGetValue(n, out waypoint already_present))
                {
                    n = already_present;
                }
                else
                {
                    // Create the neighbour if they don't already exist
                    n.point = centre(x, y, z);
                    n.point = validate_position(n.point, out bool valid);
                    n.heuristic = n.manhattan_heuristic(goal_waypoint);

                    if (!valid)
                    {
                        n.came_from = current;
                        invalid_set.Add(n);
                        return;
                    }
                }

                // Check if this is potentially a better route to the neighbour
                int tgs = current.g_score + 1;
                if (tgs < n.g_score)
                {
                    // Not a valid move, skip
                    if (!validate_move(current.point, n.point)) return;

                    // Update the path to n
                    n.came_from = current;
                    n.g_score = tgs;

                    // Re-open n
                    if (!open_set.ContainsKey(n))
                        open_set[n] = n;
                }
            });
        }
    }

    public override void draw_gizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var kv in open_set)
        {
            var w = kv.Value;
            if (w.came_from != null)
                Gizmos.DrawLine(w.point, w.came_from.point);
        }

        Gizmos.color = Color.blue;
        foreach (var c in closed_set)
            if (c.came_from != null)
                Gizmos.DrawLine(c.point, c.came_from.point);
    }

    public override string info_text()
    {
        return "Open set size = " + open_set.Count + "\n" +
               "Closed set size = " + closed_set.Count + "\n";
    }
}