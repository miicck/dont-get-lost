//#define PATH_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> An object that specifies settings for pathfinding. </summary>
public interface IPathingAgent : INotPathBlocking
{
    /// <summary> Should return the nearest valid path position
    /// to <paramref name="pos"/> that is within a distance of
    /// <see cref="resolution"/>. If no such point could be found, 
    /// <paramref name="valid"/> should be set to false. </summary>
    Vector3 validate_position(Vector3 pos, out bool valid);

    /// <summary> Should returns true if it is possible to move 
    /// between the vectors a and b (which will be nearer than
    /// <see cref="resolution"/> from each other). </summary>
    bool validate_move(Vector3 a, Vector3 b);

    /// <summary> The scale of the smallest geomtry that we 
    /// wish to be able to naviage. </summary>
    float resolution { get; }
}

public interface pathfinding_settings
{
    /// <summary> The maximum walkable ground angle. </summary>
    public float max_ground_angle();

    /// <summary> How much of the pathfinding resolution is 
    /// allowed to be filled with random stuff at the bottom. 
    /// (e.g to allow walking over small sticks etc.)</summary>
    public float ground_clearance();

    /// <summary> Should this pathfinding session be blocked by terrain? </summary>
    public bool blocked_by_terrain();
}

public class default_pathfinding_settings : pathfinding_settings
{
    public float max_ground_angle() => 45f;
    public float ground_clearance() => 0.5f;
    public bool blocked_by_terrain() => true;
}

/// <summary> Base type for all paths. </summary>
public abstract class path
{
    /// <summary> The start of the path. </summary>
    public Vector3 start { get; protected set; }

    /// <summary> The target endpoint of the path. </summary>
    public Vector3 goal { get; protected set; }

    /// <summary> The agent that will move along the path. </summary>
    public IPathingAgent agent { get; private set; }

    /// <summary> Carry out <paramref name="iterations"/> pathfinding iterations. </summary>
    public abstract void pathfind(int iterations);

    /// <summary> Perform the given number of valudation iterations, returns false if
    /// the path was found to be no longer valid. </summary>
    public virtual bool validate(int iterations) { return true; }

    /// <summary> Optimize a path, to make it more visually appealing, returns 
    /// true if some optimization was carried out </summary>
    public virtual bool optimize(int iterations) { return false; }

    /// <summary> Return the <paramref name="i"/> th point along the path. </summary>
    public abstract Vector3 this[int i] { get; }

    /// <summary> Return the length of the path (or 0 if still searching or failed). </summary>
    public abstract int length { get; }

    /// <summary> Draw information about the path. </summary>
    public virtual void draw_gizmos() { }

    /// <summary> Return information about the path. </summary>
    public virtual string info_text()
    {
        return "A path of type " + GetType().Name + "\n" +
               "State = " + state;
    }

    /// <summary> Possible path states. </summary>
    public enum STATE
    {
        SEARCHING,
        FAILED,
        COMPLETE,
        PARTIALLY_COMPLETE,
    }

    /// <summary> The current state of the path. </summary>
    public STATE state
    {
        get => _state;
        protected set
        {
            if (_state == value) return; // No change
            _state = value;
            on_state_change_listener?.Invoke(value);
        }
    }
    STATE _state;

    public delegate void on_state_change(STATE new_state);
    public on_state_change on_state_change_listener;

    public path(Vector3 start, Vector3 goal, IPathingAgent agent)
    {
        // Slightly randomize the start/goal positions to try and avoid getting
        // stuck when trying to find a valid start or end point
        this.start = start + Random.insideUnitSphere * agent.resolution / 5f;
        this.goal = goal + Random.insideUnitSphere * agent.resolution / 5f;
        this.agent = agent;
        state = STATE.SEARCHING;
    }

    public GameObject create_visualization(Color color = default)
    {
        if (length < 2) return null;
        if (color == default) color = Color.green;

        var ret = new GameObject("path");
        ret.transform.position = this[0];

        for (int i = 1; i < length; ++i)
        {
            var a = this[i - 1];
            var b = this[i];

            Vector3 delta = b - a;
            if (delta.magnitude < 10e-3)
                continue;

            var link = Resources.Load<GameObject>("misc/path_link").inst();
            link.transform.SetParent(ret.transform);
            link.transform.position = (a + b) / 2 + Vector3.up * 0.5f;
            link.transform.forward = delta;
            link.transform.localScale = new Vector3(0.1f, 0.1f, delta.magnitude);
        }

        foreach (var r in ret.GetComponentsInChildren<Renderer>())
            r.material.color = color;

        return ret;
    }
}

public class explicit_path : path
{
    List<Vector3> path;
    int last_validate_step;

    public override int length => path.Count;
    public override Vector3 this[int i] => path[i];
    public override void pathfind(int iterations) { state = STATE.COMPLETE; }

    public explicit_path(List<Vector3> path, IPathingAgent agent) : base(path[0], path[path.Count - 1], agent)
    {
        this.path = path;
        state = STATE.COMPLETE;
    }

    public override bool validate(int iterations)
    {
        switch (state)
        {
            case STATE.COMPLETE:

                if (length < 2) return true; // Nothing to validate
                last_validate_step = last_validate_step % (length - 1); // Stay in-range
                for (int i = 0; i < iterations; ++i)
                {
                    Vector3 a = this[last_validate_step];
                    Vector3 b = this[last_validate_step + 1];

                    // Note, in validation mode, we don't update the positions a and b
                    // to the return value of agent.validate_position as we only care if
                    // the positions saved to the path are still valid.
                    agent.validate_position(a, out bool valid);
                    if (!valid) return false;
                    agent.validate_position(b, out valid);
                    if (!valid) return false;
                    if (!agent.validate_move(a, b)) return false;

                    last_validate_step = (last_validate_step + 1) % (length - 1);
                }

                return true;

            case STATE.FAILED:
            case STATE.SEARCHING:
                return false;

            default:
                throw new System.Exception("Unkown path state!");
        }
    }

    public override void draw_gizmos()
    {
        Gizmos.color = Color.yellow;
        for (int i = 1; i < length; ++i)
            Gizmos.DrawLine(this[i - 1], this[i]);
    }
}



/// <summary> Carry out pathfinding using the A* algorithm. </summary>
public class astar_path : path
{
    // Once SortedSet gets a TryGet method (.NET Framework 4.7.2) 
    // these can be made into SortedSets.
    protected SortedDictionary<waypoint, waypoint> open_set;
    protected SortedDictionary<waypoint, waypoint> closed_set;
    protected waypoint start_waypoint;
    protected waypoint goal_waypoint;
    protected int endpoint_search_stage = 0;
    protected int max_iterations;
    protected int max_steps_to_startpoint;
    protected int total_iterations = 0;
    protected int last_validate_step = 0;
    protected int last_optimize_step = 0;
    protected bool accept_best_incomplete_path = false;
    protected bool require_valid_endpoint = true;

    public delegate void callback();
    public callback on_invalid_start;
    public callback on_invalid_end;

    /// <summary> The path found. </summary>
    protected List<Vector3> path;
    public override int length => path == null ? 0 : path.Count;
    public override Vector3 this[int i] => path == null ? default : path[i];

    /// <summary> The stages of a* pathfinding. </summary>
    protected enum STAGE : byte
    {
        START_SEARCH = 0, // Searching for a start point
        GOAL_SEARCH = 1,  // Searching for a goal point
        PATHFIND = 2      // Pathfinding between points
    }

    /// <summary> The current pathfinding stage. </summary>
    protected STAGE stage;

    public astar_path(Vector3 start, Vector3 goal, IPathingAgent agent,
        int max_iterations = 1000, int max_steps_to_startpoint = 2,
        bool accept_best_incomplete_path = false,
        bool require_valid_endpoint = true)
        : base(start, goal, agent)
    {
        this.max_iterations = max_iterations;
        this.max_steps_to_startpoint = max_steps_to_startpoint;
        this.accept_best_incomplete_path = accept_best_incomplete_path;
        this.require_valid_endpoint = require_valid_endpoint;

        if (!require_valid_endpoint && !accept_best_incomplete_path)
            Debug.LogError("Paths that don't require a valid endpoint should accept incomplete paths!");

        // Sort by increasing waypoint magnitude, so that the first 
        // elements of these dictionaries are closest to the goal
        open_set = new SortedDictionary<waypoint, waypoint>(new waypoint.increasing_magnitude());
        closed_set = new SortedDictionary<waypoint, waypoint>(new waypoint.increasing_magnitude());
        stage = STAGE.START_SEARCH;
    }

    protected virtual bool success(waypoint found)
    {
        return found.Equals(goal_waypoint);
    }

    protected void reconstruct_path(waypoint end,
        bool add_goal = true, bool add_start = true)
    {
        if (end == null)
        {
            state = STATE.FAILED;
            return;
        }

        path = new List<Vector3>();

        // Validate + add the move from the last waypoint to the goal
        Vector3 goal_validated = goal;
        if (add_goal) goal_validated = agent.validate_position(goal, out add_goal);
        if (add_goal && agent.validate_move(end.entrypoint, goal_validated))
            path.Add(goal_validated);

        // Add the waypoint path (which is already validated)
        path.Add(end.entrypoint);
        while (end.came_from != null)
        {
            end = end.came_from;
            path.Add(end.entrypoint);
        }

        // Validate + add the move from the first waypoint to the start
        Vector3 start_validated = start;
        if (add_start) start_validated = agent.validate_position(start, out add_start);
        if (add_start && agent.validate_move(start_validated, end.entrypoint))
            path.Add(start_validated);

        // Return the path to get start-to-finish order
        path.Reverse();
        state = STATE.COMPLETE;
        return;
    }

    public override void pathfind(int iterations)
    {
        // Check we are supposed to be searching
        if (state != STATE.SEARCHING)
            return;

        // Carry out search for endpoints
        if (stage < STAGE.PATHFIND)
        {
            search_for_endpoints(iterations);
            if (iterations < 0 || iterations >= int.MaxValue)
                pathfind(iterations);
            return;
        }

        for (int i = 0; i < iterations; ++i)
        {
            // Failure cases
            if (++total_iterations > max_iterations || open_set.Count == 0)
            {
                // Reconstruct best incomplete path
                if (accept_best_incomplete_path && closed_set.Count > 0)
                {
                    var best = closed_set.First().Value;
                    reconstruct_path(best, add_goal: false);
                    state = STATE.PARTIALLY_COMPLETE;
                    return;
                }

                state = STATE.FAILED;
                return;
            }

            // Find the lowest heuristic in the open set
            waypoint current = open_set.First().Value;

            // Check for success
            if (success(current))
            {
                reconstruct_path(current);
                return;
            }

            // Move current to closed set
            open_set.Remove(current);
            closed_set[current] = current;

            for (int j = 0; j < utils.neighbouring_dxs_3d.Length; ++j)
            {
                // Attempt to find neighbour if they alreaddy exist
                waypoint n = new waypoint(
                    current.x + utils.neighbouring_dxs_3d[j],
                    current.y + utils.neighbouring_dys_3d[j],
                    current.z + utils.neighbouring_dzs_3d[j]
                );

                // Neighbour already closed
                if (closed_set.ContainsKey(n)) continue;

                // See if the neighbour already exists, if load them instead
                if (open_set.TryGetValue(n, out waypoint already_present))
                    n = already_present;

                // Check if this is potentially a better route to the neighbour
                int tentative_distance = current.best_distance_to_start + 1;
                if (tentative_distance < n.best_distance_to_start)
                {
                    if (!can_link(current, n, out Vector3 entrypoint))
                        continue;

                    // Update the path to n
                    n.entrypoint = entrypoint;
                    n.came_from = current;
                    n.best_distance_to_start = tentative_distance;

                    // Re-open n
                    open_set[n] = n;
                }
            }
        }
    }

    protected bool can_link(waypoint current, waypoint neighbour, out Vector3 neighbour_entrypoint)
    {
        // Find a suitable entrypoint to the neighbour n from current
        if (neighbour.entrypoint == default) // Only look for an entrypoint if we don't have one already
        {
            Bounds bounds = new Bounds(grid_centre(neighbour.x, neighbour.y, neighbour.z), Vector3.one * agent.resolution);
            neighbour_entrypoint = bounds.ClosestPoint(current.entrypoint);
            neighbour_entrypoint = agent.validate_position(neighbour_entrypoint, out bool valid);
            if (!valid) return false; // Could not find a valid entrypoint
        }
        else neighbour_entrypoint = neighbour.entrypoint;

        // Not a valid move, skip
        if (!agent.validate_move(current.entrypoint, neighbour_entrypoint)) return false;

        // Update the path to n
        return true;
    }

    public override bool validate(int iterations)
    {
        switch (state)
        {
            case STATE.COMPLETE:

                if (length < 2) return true; // Nothing to validate
                last_validate_step = last_validate_step % (length - 1); // Stay in-range
                for (int i = 0; i < iterations; ++i)
                {
                    Vector3 a = this[last_validate_step];
                    Vector3 b = this[last_validate_step + 1];

                    // Note, in validation mode, we don't update the positions a and b
                    // to the return value of agent.validate_position as we only care if
                    // the positions saved to the path are still valid.
                    agent.validate_position(a, out bool valid);
                    if (!valid) return false;
                    agent.validate_position(b, out valid);
                    if (!valid) return false;
                    if (!agent.validate_move(a, b)) return false;

                    last_validate_step = (last_validate_step + 1) % (length - 1);
                }

                return true;

            case STATE.FAILED:
            case STATE.SEARCHING:
                return false;

            default:
                throw new System.Exception("Unkown path state!");
        }
    }

    public override bool optimize(int iterations)
    {
        if (length < 3) return false; // Can't optimize a streight line
        last_optimize_step = last_optimize_step % (length - 2); // Stay in-range
        bool optimized = false;

        for (int i = 0; i < iterations; ++i)
        {
            Vector3 a = path[last_optimize_step];
            Vector3 b = path[last_optimize_step + 2];

            // Remove unneccassary middle point
            if (agent.validate_move(a, b))
            {
                path.RemoveAt(last_optimize_step + 1);
                optimized = true;

                if (length < 3) // Can't optimize a streight line
                {
                    last_optimize_step = 0;
                    return true;
                }
            }

            last_optimize_step = (last_optimize_step + 1) % (length - 2);
        }

        return optimized;
    }

    protected void search_for_endpoints(int iterations)
    {
        // Check if we've already found the endpoints
        if (stage > STAGE.GOAL_SEARCH)
            return;

        // Check if we've searched too far
        if (endpoint_search_stage > max_steps_to_startpoint)
        {
            // If we don't require a valid endpoint, then we haven't failed
            if (stage == STAGE.GOAL_SEARCH && !require_valid_endpoint)
            {
                stage = STAGE.PATHFIND;
                return;
            }

            // Call invalid start/end callbacks
            switch (stage)
            {
                case STAGE.START_SEARCH:
                    on_invalid_start?.Invoke();
                    break;

                case STAGE.GOAL_SEARCH:
                    on_invalid_end?.Invoke();
                    break;
            }

            state = STATE.FAILED;
            return;
        }

        // The nearest gridpoint to the endpoint
        int x0 = 0;
        int y0 = 0;
        int z0 = 0;
        if (stage == STAGE.START_SEARCH)
            get_coordinates(start, out x0, out y0, out z0);

        // Search outward in the grid for a suitable endpoint
        for (int m = endpoint_search_stage; m <= endpoint_search_stage + iterations; ++m)
            for (int xm = 0; xm <= m; ++xm)
                for (int ym = 0; ym <= m - xm; ++ym)
                {
                    int zm = m - ym - xm;

                    // Search all combinations of x, y and z signs
                    for (int xs = -1; xs < 2; xs += 2)
                        for (int ys = -1; ys < 2; ys += 2)
                            for (int zs = -1; zs < 2; zs += 2)
                            {
                                // The grid coordinates to check
                                int x = x0 + xm * xs;
                                int y = y0 + ym * ys;
                                int z = z0 + zm * zs;

                                // Check if this grid point is valid
                                Vector3 centre = grid_centre(x, y, z);
                                centre = agent.validate_position(centre, out bool valid);
                                if (!valid) continue;

                                switch (stage)
                                {
                                    case STAGE.START_SEARCH:

                                        // Check that the move to the grid point 
                                        // from the start point is valid
                                        if (!agent.validate_move(start, centre))
                                            continue;

                                        // Set+add the start waypoint
                                        start_waypoint = new waypoint(x, y, z)
                                        {
                                            entrypoint = centre,
                                            best_distance_to_start = 0
                                        };
                                        open_set[start_waypoint] = start_waypoint;

                                        // Advance the stage
                                        endpoint_search_stage = 0;
                                        stage = STAGE.GOAL_SEARCH;
                                        return;

                                    case STAGE.GOAL_SEARCH:

                                        // Check that the move to the goal point 
                                        // from the grid point is valid
                                        if (!agent.validate_move(centre, goal))
                                            continue;

                                        // Set the goal waypoint
                                        goal_waypoint = new waypoint(x, y, z) { entrypoint = centre };

                                        // Advance the stage
                                        endpoint_search_stage = 0;
                                        stage = STAGE.PATHFIND;
                                        return;

                                    default:
                                        throw new System.Exception("Invalid pathing stage!");
                                }
                            }
                }

        // Increase the range to search
        endpoint_search_stage += iterations;
    }

    /// <summary> Convert the position <paramref name="pos"/> into gridpoint coordinates
    /// <paramref name="x"/>, <paramref name="y"/> and <paramref name="z"/>. </summary>
    protected void get_coordinates(Vector3 pos, out int x, out int y, out int z)
    {
        x = Mathf.RoundToInt((pos - goal).x / agent.resolution);
        y = Mathf.RoundToInt((pos - goal).y / agent.resolution);
        z = Mathf.RoundToInt((pos - goal).z / agent.resolution);
    }

    /// <summary> Get the location of the centre of the gridpoint at 
    /// <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>. </summary>
    protected Vector3 grid_centre(int x, int y, int z)
    {
        return goal + new Vector3(x, y, z) * agent.resolution;
    }

    /// <summary> Contains information about a gridpoint location. </summary>
    protected class waypoint
    {
        public int x { get; private set; }
        public int y { get; private set; }
        public int z { get; private set; }
        public int square_magnitude { get; private set; }

        public Vector3 entrypoint;
        public waypoint came_from;
        public int best_distance_to_start = int.MaxValue;

        /// <summary> Construct a waypoint with the given coordinates. </summary>
        public waypoint(int x, int y, int z)
        {
            this.x = x; this.y = y; this.z = z;
            this.square_magnitude = x * x + y * y + z * z;
        }

        /// <summary> Returns true if the given waypoint is
        /// at the same location as this waypoint. </summary>
        public override bool Equals(object obj)
        {
            if (obj is waypoint w)
                return w.x == x && w.y == y && w.z == z;
            return false;
        }

        /// <summary> A simple hash code (pathfinding does not actually use this - 
        /// objects are compared by the IComparers defined below) </summary>
        public override int GetHashCode()
        {
            return x ^ y ^ z;
        }

        /// <summary> Class used to sort waypoints into increasing magnitude order. </summary>
        public class increasing_magnitude : IComparer<waypoint>
        {
            public static int CompareStatic(waypoint a, waypoint b)
            {
                // First, sort by increasing magnitude
                int mc = a.square_magnitude.CompareTo(b.square_magnitude);
                if (mc != 0) return mc;

                // Then, sort by increasing coordinates
                int xc = a.x.CompareTo(b.x); if (xc != 0) return xc;
                int yc = a.y.CompareTo(b.y); if (yc != 0) return yc;
                int zc = a.z.CompareTo(b.z); if (zc != 0) return zc;

                // These are the same
                return 0;
            }

            public int Compare(waypoint a, waypoint b) { return CompareStatic(a, b); }
        }

        /// <summary> Class used to sort waypoints into decreasing magnitude order. </summary>
        public class decreasing_magnitude : IComparer<waypoint>
        {
            public int Compare(waypoint a, waypoint b)
            {
                return increasing_magnitude.CompareStatic(b, a);
            }
        }
    }

    /// <summary> Draw information about the path. </summary>
    public override void draw_gizmos()
    {
        // Draw the start + start waypoint
        Gizmos.color = start_waypoint == null ? Color.red : Color.green;
        Gizmos.DrawWireSphere(start, 0.05f);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, 1);
        if (start_waypoint != null)
            Gizmos.DrawLine(start, start_waypoint.entrypoint);

        // Draw the goal + goal waypoint
        Gizmos.color = goal_waypoint == null ? Color.red : Color.green;
        Gizmos.DrawWireSphere(goal, 0.05f);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, 1);
        if (goal_waypoint != null)
            Gizmos.DrawLine(goal, goal_waypoint.entrypoint);

        if (length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i < length; ++i)
                Gizmos.DrawLine(this[i], this[i - 1]);
            return;
        }

        Gizmos.color = Color.cyan;
        foreach (var kv in open_set)
        {
            waypoint w = kv.Value;
            if (w.came_from != null)
                Gizmos.DrawLine(w.entrypoint, w.came_from.entrypoint);
        }

        int min_mag = int.MaxValue;
        int max_mag = int.MinValue;
        foreach (var kv in closed_set)
        {
            int mag = kv.Key.square_magnitude;
            if (mag < min_mag) min_mag = mag;
            if (mag > max_mag) max_mag = mag;
        }

        Gizmos.color = Color.blue;
        foreach (var kv in closed_set)
        {
            waypoint w = kv.Value;
            float scaled = w.square_magnitude - min_mag;
            scaled /= (max_mag - min_mag);
            Gizmos.color = new Color(scaled, 0, 1 - scaled);
            if (w.came_from != null)
                Gizmos.DrawLine(w.entrypoint, w.came_from.entrypoint);
        }
    }

    /// <summary> Get information about the current path. </summary>
    public override string info_text()
    {
        return "A* path\n" +
               "Open set size = " + open_set.Count + "\n" +
               "Closed set size = " + closed_set.Count + "\n" +
               "State = " + state + "\n" +
               "Stage = " + stage + "\n" +
               "Iterations = " + total_iterations + "/" + max_iterations + "\n";
    }
}

public class random_path : astar_path
{
    public delegate bool success_func(Vector3 v);
    success_func endpoint_successful;
    success_func midpoint_successful;
    success_func midpoint_failed;
    Vector3 starting_direction;

    public random_path(Vector3 start,
        IPathingAgent agent,
        success_func endpoint_successful,
        success_func midpoint_successful = null,
        success_func midpoint_failed = null,
        Vector3 starting_direction = default) : base(start, start, agent)
    {
        // Give the start/goal a litte random boost, so we don't get stuck in loops
        Vector3 rnd = Random.insideUnitSphere * agent.resolution;
        start += rnd;
        goal += rnd;

        // Sort by decreasing distance from goal (which is set to start)
        // so that we are attempting to maximize distance from start.
        open_set = new SortedDictionary<waypoint, waypoint>(new waypoint.decreasing_magnitude());
        closed_set = new SortedDictionary<waypoint, waypoint>(new waypoint.decreasing_magnitude());
        this.endpoint_successful = endpoint_successful;
        this.midpoint_successful = midpoint_successful ?? ((v) => false);
        this.midpoint_failed = midpoint_failed ?? ((v) => false);

        this.starting_direction = starting_direction;
    }

    public override void pathfind(int iterations)
    {
        if (state != STATE.SEARCHING)
            return;

        if (stage == STAGE.START_SEARCH)
        {
            // Search for start point
            search_for_endpoints(iterations);
            return;
        }
        else if (stage == STAGE.GOAL_SEARCH)
            stage = STAGE.PATHFIND; // No goal to search for

        // Expand the path
        for (int i = 0; i < iterations; ++i)
        {
            if (open_set.Count == 0)
            {
                // Nothing left to search - check if the best
                // waypoint found is good enough as an endpoint
                var best = closed_set.First().Value;
                if (endpoint_successful(best.entrypoint))
                    reconstruct_path(best);
                else
                    state = STATE.FAILED;
                return;
            }

            waypoint current = null;
            if (open_set.Count <= utils.neighbouring_dxs_3d.Length)
            {
                if (starting_direction == default) // Randomize the starting direction
                    current = open_set.ElementAt(Random.Range(0, open_set.Count)).Value;
                else // Use specified starting direction
                    current = utils.find_to_min(open_set,
                        (ww) => -Vector3.Dot((ww.Value.entrypoint - start).normalized,
                        starting_direction)).Value;
            }
            else
                current = open_set.First().Value;

            if (midpoint_failed(current.entrypoint))
            {
                state = STATE.FAILED;
                return;
            }

            if (midpoint_successful(current.entrypoint))
            {
                reconstruct_path(current);
                return;
            }

            // Move current to closed set
            open_set.Remove(current);
            closed_set[current] = current;

            for (int j = 0; j < utils.neighbouring_dxs_3d.Length; ++j)
            {
                // Attempt to find neighbour if they alreaddy exist
                waypoint n = new waypoint(
                    current.x + utils.neighbouring_dxs_3d[j],
                    current.y + utils.neighbouring_dys_3d[j],
                    current.z + utils.neighbouring_dzs_3d[j]
                );

                // Already explored
                if (closed_set.ContainsKey(n)) continue;

                // Already open
                if (open_set.ContainsKey(n)) continue;

                // Cant link current to this neighbour
                if (!can_link(current, n, out Vector3 entrypoint)) continue;

                // Update the path to n
                n.entrypoint = entrypoint;
                n.came_from = current;

                // Re-open n
                open_set[n] = n;
            }
        }
    }

    public override string info_text()
    {
        return "RANDOM PATH\n" + base.info_text();
    }
}

public class chase_path : path
{
    astar_path base_path;
    List<Vector3> follow_path;
    Transform target;
    float goal_distance;
    bool lost_target = false;

    public astar_path.callback on_invalid_start
    {
        get => base_path.on_invalid_start;
        set => base_path.on_invalid_start = value;
    }

    public astar_path.callback on_invalid_end
    {
        get => base_path.on_invalid_end;
        set => base_path.on_invalid_end = value;
    }

    public override int length
    {
        get
        {
            chase();
            return base_path.length + follow_path.Count;
        }
    }

    public override Vector3 this[int i]
    {
        get
        {
            chase();
            if (i < base_path.length)
                return base_path[i];

            // Link up with the follow path (if the base
            // path was successful)
            if (base_path.state == STATE.COMPLETE)
                return follow_path[i - base_path.length];

            // Just clamp to the last point in the base path
            return base_path[base_path.length - 1];
        }
    }

    void chase()
    {
        if (target == null)
        {
            state = STATE.FAILED;
            return;
        }

        if (lost_target) return;

        // See if the target has moved far enough to extend the path
        Vector3 delta = target.position - follow_path[follow_path.Count - 1];
        if (delta.magnitude > goal_distance)
        {
            // Validate the new target position
            Vector3 new_pos = agent.validate_position(target.position, out bool valid);
            if (!valid)
            {
                // Invalid new posiiton
                state = STATE.PARTIALLY_COMPLETE;
                lost_target = true;
                return;
            }

            // Validate the move to the new target position
            Vector3 last_pos;
            if (follow_path.Count > 0) last_pos = follow_path[follow_path.Count - 1];
            else if (base_path.length > 0) last_pos = base_path[base_path.length - 1];
            else
            {
                Debug.LogError("This probably shouldn't be possible");
                lost_target = true;
                return;
            }

            if (!agent.validate_move(last_pos, new_pos))
            {
                // Invalid move to new position
                state = STATE.PARTIALLY_COMPLETE;
                lost_target = true;
                return;
            }

            follow_path.Add(new_pos);
        }
    }

    public chase_path(Vector3 start, Transform target, IPathingAgent agent,
        int max_iterations = 1000, float goal_distance = -1) : base(start, target.position, agent)
    {
        this.target = target;
        if (goal_distance < 0) this.goal_distance = agent.resolution;
        else this.goal_distance = goal_distance;
        base_path = new astar_path(start, target.position, agent, max_iterations: max_iterations,
            accept_best_incomplete_path: true, require_valid_endpoint: false);
        follow_path = new List<Vector3> { target.position };
        state = STATE.SEARCHING;
    }

    public override void pathfind(int iterations)
    {
        if (state != STATE.SEARCHING)
            return;

        if (target == null)
        {
            state = STATE.FAILED;
            return;
        }

        base_path.pathfind(iterations);
        state = base_path.state;
    }

    public override void draw_gizmos()
    {
        base_path?.draw_gizmos();

        Gizmos.color = Color.cyan;
        for (int i = 1; i < follow_path.Count; ++i)
            Gizmos.DrawLine(follow_path[i], follow_path[i - 1]);
    }

    public override string info_text()
    {
        return base_path?.info_text();
    }
}

public class flee_path : astar_path
{
    public flee_path(Vector3 start, Transform fleeing, IPathingAgent agent, int max_iterations = 1000)
         : base(start, fleeing.position, agent, max_iterations: max_iterations)
    {
        open_set = new SortedDictionary<waypoint, waypoint>(new increasing_hash_code());
    }

    protected override bool success(waypoint found)
    {
        return found.best_distance_to_start > 10;
    }

    /// <summary> Class to order waypoints by their hash code. </summary>
    class increasing_hash_code : IComparer<waypoint>
    {
        public int Compare(waypoint a, waypoint b) { return b.GetHashCode().CompareTo(a.GetHashCode()); }
    }
}

/// <summary> This object, or it's children, do not block paths. </summary>
public interface INotPathBlocking { }

/// <summary> Tools to use in pathfinding. </summary>
public static class pathfinding_utils
{
    /// <summary> Find a valid position for a walking agent operating on
    /// a grid of the given resolution, by using a box cast to check
    /// for grounding within the gridpoint with the given 
    /// <paramref name="centre"/>. </summary>
    static Vector3 boxcast_position_validate(Vector3 centre, float resolution,
        out bool valid, pathfinding_settings settings = null)
    {
        if (settings == null)
            settings = new default_pathfinding_settings();

        Vector3 size = Vector3.one * resolution;
        Vector3 start_pos = centre + Vector3.up * resolution;
        Vector3 end_pos = centre;
        Vector3 move = end_pos - start_pos;

        foreach (var h in Physics.BoxCastAll(start_pos, size / 2f,
           move.normalized, Quaternion.identity, move.magnitude))
            if (h.transform.GetComponentInParent<INotPathBlocking>() == null)
            {
                // BoxCastAll returns h.point = [0,0,0] if the collider
                // was already inside the starting box position
                if (h.point == default) continue;

                if (Vector3.Angle(h.normal, Vector3.up) > settings.max_ground_angle())
                    continue; // Too steep

                valid = true;
                return h.point;
            }

        valid = false;
        return centre;
    }

    /// <summary> The same as <see cref="boxcast_position_validate(Vector3, float, out bool)"/>, 
    /// but using an overlap box instead of a boxcast. </summary>
    static Vector3 overap_box_position_validate(Vector3 centre, float resolution,
        out bool valid)
    {
        foreach (var c in Physics.OverlapBox(centre, Vector3.one * resolution / 2f))
            if (c.transform.GetComponentInParent<INotPathBlocking>() == null)
            {
                valid = true;
                return c.ClosestPoint(centre);
            }

        valid = false;
        return centre;
    }

    /// <summary> Check that, during the course of a move from <paramref name="a"/> 
    /// to <paramref name="b"/> a walking agent of the given <paramref name="width"/>
    /// will always have sufficient grounding. </summary>
    static bool validate_move_grounding(Vector3 a, Vector3 b,
        float width, float ground_clearance)
    {
        Vector3 delta = b - a;
        for (float p = 0; p <= delta.magnitude; p += width)
        {
            Vector3 middle = a + delta.normalized * p;
            Vector3 start = middle + Vector3.up * ground_clearance;
            Vector3 end = middle - Vector3.up * ground_clearance;
            Vector3 delta_ray = end - start;

            bool grounding_found = false;
            foreach (var h in Physics.RaycastAll(start, delta_ray.normalized, delta_ray.magnitude))
                if (h.transform.GetComponentInParent<INotPathBlocking>() == null)
                {
                    grounding_found = true;
                    break;
                }

            if (!grounding_found)
                return false;
        }

        return true;
    }

    /// <summary> Same as <see cref="validate_move_grounding(Vector3, Vector3, float, float, Transform)"/>,
    /// but using overlap boxes rather than raycasts. </summary>
    static bool validate_move_grounding_overlap(Vector3 a, Vector3 b,
        float width, float ground_clearance)
    {
        Vector3 delta = b - a;
        if (delta.magnitude < 1e-4) return true;

        Vector3 forward = delta.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 up = Vector3.Cross(right, forward);
        if (up.magnitude < 1e-4) up = Vector3.up;
        Quaternion orientation = Quaternion.LookRotation(forward, up);

        for (float p = 0; p <= delta.magnitude; p += width)
        {
            Vector3 centre = a + delta.normalized * (p + width / 2f) + Vector3.up * ground_clearance;
            Vector3 size = new Vector3(width, 2 * ground_clearance, width);
            foreach (var c in Physics.OverlapBox(centre, size / 2f, orientation))
                if (c.transform.GetComponentInParent<INotPathBlocking>() == null)
                    return true;
        }

        return false;
    }

    /// <summary> Check that nothing is in the way on a from <paramref name="a"/> 
    /// to <paramref name="b"/> for an agent of the given <paramref name="width"/>,
    /// <paramref name="height"/> and <paramref name="ground_clearance"/>, by 
    /// checking if anythging overlaps an appropriately-shaped box. </summary>
    static bool validate_move_overlap(Vector3 a, Vector3 b,
        float width, float height, out string reason, pathfinding_settings settings = null)
    {
        if (settings == null)
            settings = new default_pathfinding_settings();

        float ground_clearance = settings.ground_clearance() * width;

        reason = null;
        Vector3 delta = b - a;
        if (delta.magnitude < 1e-4) return true;

        Vector3 centre = a + delta / 2f + Vector3.up * (height / 2 + ground_clearance / 2);
        Vector3 size = new Vector3(width, height - ground_clearance, delta.magnitude);

        Vector3 forward = delta.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 up = Vector3.Cross(right, forward);
        if (up.magnitude < 1e-4) up = Vector3.up;
        Quaternion orientation = Quaternion.LookRotation(forward, up);

        foreach (var c in Physics.OverlapBox(centre, size / 2f, orientation))
            if (c.transform.GetComponentInParent<INotPathBlocking>() == null)
            {
                if (!settings.blocked_by_terrain() &&
                    c.transform.GetComponentInParent<Terrain>())
                    continue;

                reason = "blocked by " + c.gameObject.name;
                return false;
            }

        return true;
    }

    /// <summary> Validate a move from <paramref name="a"/> to <paramref name="b"/> for an
    /// agent with the given <paramref name="width"/>, <paramref name="height"/> and 
    /// <paramref name="ground_clearance"/> walking. </summary>
    public static bool validate_walking_move(Vector3 a, Vector3 b,
        float width, float height, out string reason, pathfinding_settings settings = null)
    {
        if (settings == null)
            settings = new default_pathfinding_settings();

        Vector3 delta = b - a;
        Vector3 delta_xz = delta; delta_xz.y = 0;
        if (Vector3.Angle(delta, delta_xz) > settings.max_ground_angle())
        {
            reason = "Too steep";
            return false;
        }

        bool overlap_test = validate_move_overlap(a, b, width, height, out reason, settings: settings);
        if (!overlap_test) return false;

        bool grounding = validate_move_grounding(a, b, width, settings.ground_clearance() * width);
        if (!grounding)
        {
            reason = "No grounding";
            return false;
        }
        return true;
    }

    // Overload of the above without the reason
    public static bool validate_walking_move(Vector3 a, Vector3 b,
    float width, float height, pathfinding_settings settings = null)
    {
        return validate_walking_move(a, b, width, height, out string reason, settings: settings);
    }

    /// <summary> Validate the location <paramref name="v"/> for a walking agent. </summary>
    public static Vector3 validate_walking_position(Vector3 v, float resolution,
        out bool valid, pathfinding_settings settings = null)
    {
        return boxcast_position_validate(v, resolution, out valid, settings: settings);
    }
}