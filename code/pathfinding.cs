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

    /// <summary> Optimize a path, to make it more visually appealing. </summary>
    public virtual void optimize(int iterations) { }

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
        COMPLETE
    }

    /// <summary> The current state of the path. </summary>
    public STATE state { get; protected set; }

    public path(Vector3 start, Vector3 goal, IPathingAgent agent)
    {
        this.start = start;
        this.goal = goal;
        this.agent = agent;
        state = STATE.SEARCHING;
    }
}



/// <summary> Carry out pathfinding using the A* algorithm. </summary>
public class astar_path : path
{
    protected SortedDictionary<waypoint, waypoint> open_set;
    protected HashSet<waypoint> closed_set;
    protected waypoint start_waypoint;
    protected waypoint goal_waypoint;
    protected int endpoint_search_stage = 0;
    protected int max_iterations;
    protected int total_iterations = 0;
    protected int last_validate_step = 0;
    protected int last_optimize_step = 0;

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

    public astar_path(Vector3 start, Vector3 goal, IPathingAgent agent, int max_iterations = 1000)
        : base(start, goal, agent)
    {
        this.max_iterations = max_iterations;
        open_set = new SortedDictionary<waypoint, waypoint>(new increasing_hash_code());
        closed_set = new HashSet<waypoint>();
        stage = STAGE.START_SEARCH;
    }

    protected void reconstruct_path(waypoint end)
    {
        if (end == null)
        {
            state = STATE.FAILED;
            return;
        }

        if (agent.validate_move(end.entrypoint, goal))
            path = new List<Vector3> { goal, end.entrypoint };
        else
            path = new List<Vector3> { end.entrypoint };

        while (end.came_from != null)
        {
            end = end.came_from;
            path.Add(end.entrypoint);
        }

        if (agent.validate_move(start, end.entrypoint))
            path.Add(start);

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
            if (++total_iterations > max_iterations)
            {
                state = STATE.FAILED;
                return;
            }

            if (open_set.Count == 0)
            {
                state = STATE.FAILED;
                return;
            }

            // Find the lowest heuristic in the open set
            waypoint current = open_set.First().Value;

            // Check for success
            if (current.Equals(goal_waypoint))
            {
                reconstruct_path(current);
                return;
            }

            // Move current to closed set
            open_set.Remove(current);
            closed_set.Add(current);

            for (int j = 0; j < utils.neighbouring_dxs_3d.Length; ++j)
            {
                // Attempt to find neighbour if they alreaddy exist
                waypoint n = new waypoint(
                    current.x + utils.neighbouring_dxs_3d[j],
                    current.y + utils.neighbouring_dys_3d[j],
                    current.z + utils.neighbouring_dzs_3d[j]
                );

                // Neighbour already closed
                if (closed_set.Contains(n)) continue;

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

    public override void optimize(int iterations)
    {
        if (length < 3) return; // Can't optimize a streight line
        last_optimize_step = last_optimize_step % (length - 2); // Stay in-range

        for (int i = 0; i < iterations; ++i)
        {
            Vector3 a = path[last_optimize_step];
            Vector3 b = path[last_optimize_step + 2];

            // Remove unneccassary middle point
            if (agent.validate_move(a, b))
                path.RemoveAt(last_optimize_step + 1);

            last_optimize_step = (last_optimize_step + 1) % (length - 2);
        }
    }

    protected void search_for_endpoints(int iterations)
    {
        // Check if we've already found the endpoints
        if (stage > STAGE.GOAL_SEARCH)
            return;

        // Check if we've searched too far
        if (endpoint_search_stage > 32)
        {
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

                                // Found a valid point, move to the next pathfinding stage
                                endpoint_search_stage = 0;
                                if (stage == STAGE.START_SEARCH)
                                {
                                    start_waypoint = new waypoint(x, y, z)
                                    {
                                        entrypoint = centre,
                                        best_distance_to_start = 0
                                    };
                                    open_set[start_waypoint] = start_waypoint;
                                    stage = STAGE.GOAL_SEARCH;
                                }
                                else if (stage == STAGE.GOAL_SEARCH)
                                {
                                    goal_waypoint = new waypoint(x, y, z) { entrypoint = centre };
                                    stage = STAGE.PATHFIND;
                                }
                                return;
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

        public Vector3 entrypoint;
        public waypoint came_from;
        public int best_distance_to_start = int.MaxValue;

        /// <summary> Construct a waypoint with the given coordinates. </summary>
        public waypoint(int x, int y, int z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        /// <summary> Returns true if the given waypoint is
        /// at the same location as this waypoint. </summary>
        public override bool Equals(object obj)
        {
            if (obj is waypoint w)
                return w.x == x && w.y == y && w.z == z;
            return false;
        }

        /// <summary> A unique hash code that increases as we move away 
        /// from the origin. This means that it can be used as both a 
        /// heuristic and as a location in a hash table. </summary>
        public override int GetHashCode()
        {
            int xh = x >= 0 ? 2 * x : 2 * (-x) - 1;
            int yh = y >= 0 ? 2 * y : 2 * (-y) - 1;
            int zh = z >= 0 ? 2 * z : 2 * (-z) - 1;
            int mxy = xh > yh ? xh : yh;
            int m = mxy > zh ? mxy : zh;
            int s = m * m * m;
            if (mxy == m) return s + xh + (m - yh) + (2 * m + 1) * z;
            else return s + (2 * m + 1) * (m + 1) + mxy * (mxy + 1) + yh - xh;
        }
    }

    /// <summary> Class to order waypoints by their hash code. </summary>
    class increasing_hash_code : IComparer<waypoint>
    {
        public int Compare(waypoint a, waypoint b) { return a.GetHashCode().CompareTo(b.GetHashCode()); }
    }

    /// <summary> Draw information about the path. </summary>
    public override void draw_gizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(start, 0.1f);
        Gizmos.DrawSphere(goal, 0.1f);
        if (start_waypoint != null) Gizmos.DrawWireSphere(start_waypoint.entrypoint, 0.1f);
        if (goal_waypoint != null) Gizmos.DrawWireSphere(goal_waypoint.entrypoint, 0.1f);

        if (length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i < length; ++i)
                Gizmos.DrawLine(this[i], this[i - 1]);
            return;
        }

        Gizmos.color = Color.cyan;
        foreach (KeyValuePair<waypoint, waypoint> kv in open_set)
        {
            waypoint w = kv.Value;
            if (w.came_from != null)
                Gizmos.DrawLine(w.entrypoint, w.came_from.entrypoint);
        }

        Gizmos.color = Color.blue;
        foreach (waypoint w in closed_set)
            if (w.came_from != null)
                Gizmos.DrawLine(w.entrypoint, w.came_from.entrypoint);
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
    protected waypoint current;

    public delegate bool success_func(Vector3 v);
    success_func endpoint_successful;
    success_func midpoint_successful;

    public random_path(Vector3 start,
        success_func midpoint_successful, success_func endpoint_successful,
        IPathingAgent agent) : base(start, start, agent)
    {
        // Give the start/goal a litte random boost, so we don't get stuck in  loops
        Vector3 rnd = Random.insideUnitSphere * agent.resolution;
        start += rnd;
        goal += rnd;

        // Sort by decreasing distance from goal (which is set to start)
        // so that we are attempting to maximize distance from start.
        open_set = new SortedDictionary<waypoint, waypoint>(new decreasing_hash_code());
        this.endpoint_successful = endpoint_successful;
        this.midpoint_successful = midpoint_successful;
    }

    /// <summary> Class to order waypoints by the negative of their hash code. </summary>
    class decreasing_hash_code : IComparer<waypoint>
    {
        public int Compare(waypoint a, waypoint b) { return b.GetHashCode().CompareTo(a.GetHashCode()); }
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
                if (current != null && endpoint_successful(current.entrypoint))
                    reconstruct_path(current);
                else
                    state = STATE.FAILED;
                return;
            }
            else if (open_set.Count <= utils.neighbouring_dxs_3d.Length)
            {
                // Randomize the starting direction
                current = open_set.ElementAt(Random.Range(0, open_set.Count)).Value;
            }
            else
                current = open_set.First().Value;

            if (midpoint_successful(current.entrypoint))
            {
                reconstruct_path(current);
                return;
            }

            // Move current to closed set
            open_set.Remove(current);
            closed_set.Add(current);

            for (int j = 0; j < utils.neighbouring_dxs_3d.Length; ++j)
            {
                // Attempt to find neighbour if they alreaddy exist
                waypoint n = new waypoint(
                    current.x + utils.neighbouring_dxs_3d[j],
                    current.y + utils.neighbouring_dys_3d[j],
                    current.z + utils.neighbouring_dzs_3d[j]
                );

                // Already explored
                if (closed_set.Contains(n)) continue;

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

            return follow_path[i - base_path.length];
        }
    }

    void chase()
    {
        if (target == null)
        {
            state = STATE.FAILED;
            return;
        }

        // See if the target has moved far enough to extend the path
        Vector3 delta = target.position - follow_path[follow_path.Count - 1];
        if (delta.magnitude > goal_distance)
            follow_path.Add(target.position);
    }

    public chase_path(Vector3 start, Transform target, IPathingAgent agent,
        int max_iterations = 1000, float goal_distance = -1) : base(start, target.position, agent)
    {
        this.target = target;
        if (goal_distance < 0) this.goal_distance = agent.resolution;
        else this.goal_distance = goal_distance;
        base_path = new astar_path(start, target.position, agent, max_iterations: max_iterations);
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
        out bool valid)
    {
        Vector3 size = Vector3.one * resolution;
        Vector3 start_pos = centre + Vector3.up * resolution;
        Vector3 end_pos = centre;

        Vector3 move = end_pos - start_pos;
        foreach (var h in Physics.BoxCastAll(start_pos, size / 2f,
           move.normalized, Quaternion.identity, move.magnitude))
            if (h.transform.GetComponentInParent<INotPathBlocking>() == null)
            {
                valid = true;
                if (h.point == default) return centre;
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
                    grounding_found = true;

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
        float width, float height, float ground_clearance, out string reason)
    {
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
                reason = "blocked by " + c.gameObject.name;
                return false;
            }

        return true;
    }

    /// <summary> Validate a move from <paramref name="a"/> to <paramref name="b"/> for an
    /// agent with the given <paramref name="width"/>, <paramref name="height"/> and 
    /// <paramref name="ground_clearance"/> walking. </summary>
    public static bool validate_walking_move(Vector3 a, Vector3 b,
        float width, float height, float ground_clearance, out string reason)
    {
        bool overlap_test = validate_move_overlap(a, b, width, height, ground_clearance, out reason);
        if (!overlap_test) return false;

        bool grounding = validate_move_grounding(a, b, width, ground_clearance);
        if (!grounding)
        {
            reason = "No grounding";
            return false;
        }
        return true;
    }

    // Overload of the above without the reason
    public static bool validate_walking_move(Vector3 a, Vector3 b,
    float width, float height, float ground_clearance)
    {
        return validate_walking_move(a, b, width, height, ground_clearance, out string reason);
    }

    /// <summary> Validate the location <paramref name="v"/> for a walking agent. </summary>
    public static Vector3 validate_walking_position(Vector3 v, float resolution,
        out bool valid)
    {
        return boxcast_position_validate(v, resolution, out valid);
    }
}