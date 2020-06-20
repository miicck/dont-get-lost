//#define PATH_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> An object that specifies settings for pathfinding. </summary>
public interface IPathingAgent
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
    public Vector3 start { get; private set; }

    /// <summary> The target endpoint of the path. </summary>
    public Vector3 goal { get; private set; }

    /// <summary> The agent that will move along the path. </summary>
    public IPathingAgent agent { get; private set; }

    /// <summary> Carry out <paramref name="iterations"/> pathfinding iterations. </summary>
    public abstract void pathfind(int iterations);

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
    SortedDictionary<waypoint, waypoint> open_set;
    HashSet<waypoint> closed_set;
    waypoint start_waypoint;
    waypoint goal_waypoint;
    int endpoint_search_stage = 0;
    int max_iterations;
    int total_iterations = 0;

    /// <summary> The path found. </summary>
    public List<Vector3> path;
    public override int length => path == null ? 0 : path.Count;
    public override Vector3 this[int i] => path == null ? default : path[i];

    /// <summary> The stages of a* pathfinding. </summary>
    enum STAGE : byte
    {
        START_SEARCH = 0, // Searching for a start point
        GOAL_SEARCH = 1,  // Searching for a goal point
        PATHFIND = 2      // Pathfinding between points
    }

    /// <summary> The current pathfinding stage. </summary>
    STAGE stage;

    public astar_path(Vector3 start, Vector3 goal, IPathingAgent agent, int max_iterations = 1000)
        : base(start, goal, agent)
    {
        this.max_iterations = max_iterations;
        open_set = new SortedDictionary<waypoint, waypoint>(new waypoint_comp());
        closed_set = new HashSet<waypoint>();
        stage = STAGE.START_SEARCH;
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
                path = new List<Vector3>();
                while (current.came_from != null)
                {
                    path.Add(current.entrypoint);
                    current = current.came_from;
                }
                path.Reverse();
                state = STATE.COMPLETE;
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
                    // Find a suitable entrypoint to the neighbour n from current
                    Bounds bounds = new Bounds(grid_centre(n.x, n.y, n.z), Vector3.one * agent.resolution);
                    Vector3 entrypoint = agent.validate_position(bounds.ClosestPoint(current.entrypoint), out bool valid);
                    if (!valid) continue; // Could not find a valid entrypoint

                    // Not a valid move, skip
                    if (!agent.validate_move(current.entrypoint, entrypoint)) continue;

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

    void search_for_endpoints(int iterations)
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
    class waypoint
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

    /// <summary> Class to compare two waypoints by their hash code. </summary>
    class waypoint_comp : IComparer<waypoint>
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

/// <summary> Tools to use in pathfinding. </summary>
public static class pathfinding_utils
{
    /// <summary> Find a valid position for a walking agent operating on
    /// a grid of the given resolution, by using a box cast to check
    /// for grounding within the gridpoint with the given 
    /// <paramref name="centre"/>. </summary>
    static Vector3 boxcast_position_validate(Vector3 centre, float resolution, out bool valid)
    {
        Vector3 size = Vector3.one * resolution;
        Vector3 start_pos = centre + Vector3.up * resolution;
        Vector3 end_pos = centre;

        Vector3 move = end_pos - start_pos;
        valid = Physics.BoxCast(start_pos, size / 2f, move.normalized,
            out RaycastHit hit, Quaternion.identity, move.magnitude);

        if (!valid) return centre;
        return hit.point + Vector3.up / 100f;
    }

    /// <summary> The same as <see cref="boxcast_position_validate(Vector3, float, out bool)"/>, 
    /// but using an overlap box instead of a boxcast. </summary>
    static Vector3 overap_box_position_validate(Vector3 centre, float resolution, out bool valid)
    {
        foreach (var c in Physics.OverlapBox(centre, Vector3.one * resolution / 2f))
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
            if (!Physics.Raycast(start, delta_ray.normalized, delta_ray.magnitude))
                return false;
        }

        return true;
    }

    /// <summary> Check that nothing is in the way on a from <paramref name="a"/> 
    /// to <paramref name="b"/> for an agent of the given <paramref name="width"/>,
    /// <paramref name="height"/> and <paramref name="ground_clearance"/>, by 
    /// checking if anythging overlaps an appropriately-shaped box. </summary>
    static bool validate_move_overlap(Vector3 a, Vector3 b,
        float width, float height, float ground_clearance)
    {
        Vector3 delta = b - a;
        if (delta.magnitude < 1e-4) return true;

        Vector3 centre = a + delta / 2f + Vector3.up * (height / 2 + ground_clearance / 2);
        Vector3 size = new Vector3(width, height - ground_clearance, delta.magnitude);

        Vector3 forward = delta.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 up = Vector3.Cross(right, forward);
        if (up.magnitude < 1e-4) up = Vector3.up;
        Quaternion orientation = Quaternion.LookRotation(forward, up);

        return Physics.OverlapBox(centre, size / 2f, orientation).Length == 0;
    }

    /// <summary> Validate a move from <paramref name="a"/> to <paramref name="b"/> for an
    /// agent with the given <paramref name="width"/>, <paramref name="height"/> and 
    /// <paramref name="ground_clearance"/> walking. </summary>
    public static bool validate_walking_move(Vector3 a, Vector3 b,
        float width, float height, float ground_clearance)
    {
        return validate_move_overlap(a, b, width, height, ground_clearance) &&
               validate_move_grounding(a, b, width, ground_clearance);
    }

    /// <summary> Validate the location <paramref name="v"/> for a walking agent. </summary>
    public static Vector3 validate_walking_position(Vector3 v, float resolution, out bool valid)
    {
        return boxcast_position_validate(v, resolution, out valid);
    }
}