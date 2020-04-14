using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class path
{
    // The origin and size of pathfinding grid
    Vector3 origin;
    int size;

    // The approximate size of objects that can be pathed over
    float ground_clearance;

    // The resolution of the pathfinding grid
    float resolution;

    // A point on the pathfinding grid
    class point
    {
        int x; int y; int z;                           // The pathfinding grid coordinates
        path path;                                     // The path this point is in
        public Vector3 grounding { get; private set; } // The grounding on geometry

        // Variables used for pathfinding
        public float gscore = float.PositiveInfinity;
        public float fscore = float.PositiveInfinity;
        public point came_from;

        public static point create(int x, int y, int z, path p)
        {
            // Check we're in range
            if (x < 0 || y < 0 || z < 0) return null;
            if (x >= p.size || y >= p.size || z >= p.size) return null;

            // Find grounding, return null if no grounding found
            Vector3 vec = p.origin + new Vector3(x, y, z) * p.resolution;
            RaycastHit hit;
            if (!Physics.Raycast(vec + Vector3.up * 0.5f * p.resolution,
                -Vector3.up, out hit, p.resolution)) return null;

            // Creat the point
            return new point()
            {
                x = x,
                y = y,
                z = z,
                path = p,
                grounding = hit.point
            };
        }

        // Private constructor, points can only be 
        // created via the create() function
        private point() { }

        public List<point> neighbours
        {
            get
            {
                List<point> ret = new List<point>();
                Vector3 gc = Vector3.up * path.ground_clearance;

                for (int n = 0; n < utils.neighbouring_dxs_3d.Length; ++n)
                {
                    int dx = utils.neighbouring_dxs_3d[n];
                    int dy = utils.neighbouring_dys_3d[n];
                    int dz = utils.neighbouring_dzs_3d[n];

                    // Try to create the neighbouring point
                    var p = create(x + dx, y + dy, z + dz, path);
                    if (p == null) continue;

                    // Check if the next point is accesible from this point
                    Vector3 a = grounding + gc;
                    Vector3 b = p.grounding + gc;
                    if (Physics.Raycast(a, b - a, (b - a).magnitude)) continue;

                    // Add the accesible neighbour
                    ret.Add(p);
                }
                return ret;
            }
        }

        public float heuristic(point other)
        {
            // Manhattan hueristic
            return Mathf.Abs(x - other.x) +
                   Mathf.Abs(z - other.z) +
                   Mathf.Abs(y - other.y);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;
            var p = (point)obj;
            return p.x == x && p.y == y && p.z == z;
        }

        public override int GetHashCode()
        {
            return x + y * path.size + z * path.size * path.size;
        }
    }

    point grid_point(Vector3 v)
    {
        // Convert a vector to a point
        Vector3 delta = v - origin;
        int x = Mathf.RoundToInt(delta.x / resolution);
        int y = Mathf.RoundToInt(delta.y / resolution);
        int z = Mathf.RoundToInt(delta.z / resolution);
        return point.create(x, y, z, this);
    }

    List<Vector3> calculated_path;
    HashSet<point> open;
    HashSet<point> closed;
    point goal;

    public path(Vector3 start, Vector3 end, float ground_clearance = 0.5f, float resolution = 0.5f)
    {
        this.ground_clearance = Mathf.Clamp(ground_clearance, 0, resolution);
        this.resolution = resolution;

        // Work out the size/origin of the coordinate system
        Vector3 min = utils.min(start, end);
        Vector3 max = utils.max(start, end);
        Vector3 delta = max - min;
        size = (int)(2f * Mathf.Max(delta.x, delta.y, delta.z) / resolution);
        if (size * size * size > int.MaxValue) return;
        origin = utils.round((start + end) / 2f - Vector3.one * size * resolution / 2f);

        // Initialize pathfinding
        var start_p = grid_point(start);
        goal = grid_point(end);

        if (start_p == null || goal == null) return;

        open = new HashSet<point> { start_p };
        closed = new HashSet<point> { };
        start_p.fscore = start_p.heuristic(goal);
        start_p.gscore = 0;
    }

    public void run_pathfinding(int iterations)
    {
        int iter = 0;
        while (open != null && open.Count > 0)
        {
            if (++iter > iterations) break;
            var current = utils.find_to_min(open, (p) => p.fscore);

            if (current.Equals(goal))
            {
                // Reconstruct the path
                calculated_path = new List<Vector3> { current.grounding };
                while (current.came_from != null)
                {
                    current = current.came_from;
                    calculated_path.Add(current.grounding);
                }
                calculated_path.Reverse();
                return;
            }

            open.Remove(current);
            closed.Add(current);

            foreach (var n in current.neighbours)
            {
                float tgs = current.gscore + 1f;
                if (tgs < n.gscore)
                {
                    n.came_from = current;
                    n.gscore = tgs;
                    n.fscore = tgs + n.heuristic(goal);
                    if (!closed.Contains(n))
                        open.Add(n);
                }
            }
        }
    }

    public void draw_gizmos()
    {
        // Draw the coordinate system range
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin + Vector3.one * size * resolution / 2f,
                            Vector3.one * size * resolution);

        if (calculated_path != null)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i < calculated_path.Count; ++i)
                Gizmos.DrawLine(calculated_path[i - 1], calculated_path[i]);
        }

        if (open != null)
        {
            Gizmos.color = new Color(0, 1f, 1f, 0.5f);
            foreach (var p in open)
                Gizmos.DrawWireCube(p.grounding, Vector3.one * resolution);
        }

        if (closed != null)
        {
            Gizmos.color = new Color(0, 0, 1f, 0.5f);
            foreach (var p in closed)
                Gizmos.DrawWireCube(p.grounding, Vector3.one * resolution);
        }
    }
}


public class path_old
{
    // We use a coordinate system specific to this
    // path instance, so that we can hash the coordinates
    // within that system.
    int coordinate_range;   // The range of the waypoint coordinates
    Vector3 origin;         // The origin of the waypoint coordinates
    float ground_clearance; // The (qpproximate) size of small objects which can be walked over

    // Returns true if x is in the path coordinate range
    bool in_range(int x) { return x >= 0 && x < coordinate_range; }

    // A point on the aformentioned coordinate system
    class waypoint
    {
        int x; int y; int z;                                 // Coordinates in path coordinate system
        public Vector3 grounding_point { get; private set; } // The point on the ground at this waypoint
        public bool valid { get; private set; }              // Is this waypoint valid

        public waypoint came_from = null;
        public float gscore = float.PositiveInfinity;
        public float fscore = float.PositiveInfinity;

        path_old path;

        public static waypoint create(Vector3 v, path_old p)
        {
            int x = Mathf.RoundToInt(v.x - p.origin.x);
            int y = Mathf.RoundToInt(v.y - p.origin.y);
            int z = Mathf.RoundToInt(v.z - p.origin.z);

            if (!p.in_range(x) || !p.in_range(y) || !p.in_range(z))
                return null;

            Vector3 ray_start = p.origin + new Vector3(x, y + 0.5f, z);
            RaycastHit hit;

            if (Physics.Raycast(ray_start, -Vector3.up, out hit, 1f))
                return new waypoint(x, y, z, p)
                {
                    grounding_point = hit.point
                };

            return null;
        }

        private waypoint(int x, int y, int z, path_old p)
        {
            this.x = x; this.y = y; this.z = z;
            path = p;
        }

        public List<waypoint> neighbours()
        {
            List<waypoint> ret = new List<waypoint>();
            for (int dx = -1; dx < 2; ++dx)
                for (int dy = -1; dy < 2; ++dy)
                    for (int dz = -1; dz < 2; ++dz)
                    {
                        if (dx == 0 &&
                            dy == 0 &&
                            dz == 0) continue;

                        var w = waypoint.create(grounding_point + new Vector3(dx, dy, dz), path);
                        if (w == null) continue;
                        if (!can_path_to(w)) continue;

                        ret.Add(w);
                    }

            return ret;
        }

        bool can_path_to(waypoint neighbour)
        {
            Vector3 a = grounding_point + Vector3.up * path.ground_clearance;
            Vector3 b = neighbour.grounding_point + Vector3.up * path.ground_clearance;
            return !Physics.Raycast(a, b - a, (b - a).magnitude);
        }

        public float heuristic(waypoint other)
        {
            return Mathf.Abs(x - other.x) +
                   Mathf.Abs(y - other.y) +
                   Mathf.Abs(z - other.z);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (GetType() != obj.GetType()) return false;
            waypoint p = (waypoint)obj;
            return x == p.x &&
                   y == p.y &&
                   z == p.z;
        }

        public override int GetHashCode()
        {
            return x + y * path.coordinate_range + z * path.coordinate_range * path.coordinate_range;
        }

        public void draw_gizmos()
        {
            Gizmos.color = Color.red;
            if (valid) Gizmos.color = Color.green;
            Vector3 centre = path.origin + new Vector3(x, y, z);
            Gizmos.DrawWireCube(centre, Vector3.one);
        }
    }

    public path_old(Vector3 start, Vector3 end, float ground_clearance = 0.5f)
    {
        this.ground_clearance = ground_clearance;

        Vector3 min = new Vector3(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Min(start.z, end.z)
        );

        Vector3 max = new Vector3(
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y),
            Mathf.Max(start.z, end.z)
        );

        Vector3 delta = max - min;

        // The coordinate system
        coordinate_range = Mathf.Max(
            (int)(2f * delta.x),
            (int)(2f * delta.y),
            (int)(2f * delta.z)
        );
        origin = (start + end) / 2f - Vector3.one * coordinate_range / 2f;

        get_path(waypoint.create(start, this),
                 waypoint.create(end, this));
    }

    // Get the path from start to goal, using the A* algorithm
    List<waypoint> calculated_path;
    void get_path(waypoint start, waypoint goal)
    {
        if (start == null || goal == null) return;
        if (!start.valid || !goal.valid) return;

        var open = new HashSet<waypoint> { start };
        var closed = new HashSet<waypoint> { };

        start.gscore = 0;
        start.fscore = start.heuristic(goal);

        while (open.Count > 0)
        {
            var current = utils.find_to_min(open, (w) => w.fscore);
            if (current.Equals(goal))
            {
                calculated_path = new List<waypoint> { current };
                while (current.came_from != null)
                {
                    current = current.came_from;
                    calculated_path.Add(current);
                }
                return;
            }

            closed.Add(current);
            open.Remove(current);

            foreach (var n in current.neighbours())
            {
                float tgs = current.gscore + 1f;
                if (tgs < n.gscore)
                {
                    n.came_from = current;
                    n.gscore = tgs;
                    n.fscore = tgs + n.heuristic(goal);
                    if (!closed.Contains(n))
                        open.Add(n);
                }
            }
        }
    }

    public void draw_gizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin + Vector3.one * coordinate_range / 2, Vector3.one * coordinate_range);

        if (calculated_path == null) return;
        for (int i = 1; i < calculated_path.Count; ++i)
            Gizmos.DrawLine(calculated_path[i].grounding_point,
                            calculated_path[i - 1].grounding_point);
    }
}