using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class path
{
    // The origin and size of pathfinding grid
    Vector3 origin;
    int size_x;
    int size_y;
    int size_z;

    // The approximate size of objects that can be pathed over
    float ground_clearance;

    // The resolution of the pathfinding grid in the x,y,z directions
    Vector3 resolution;

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
            if (x >= p.size_x || y >= p.size_y || z >= p.size_z) return null;

            // Find grounding, return null if no grounding found
            Vector3 vec = p.origin + new Vector3(
                x * p.resolution.x,
                y * p.resolution.y,
                z * p.resolution.z);

            // Returns null if the test point fails the constraint
            if (!p.constraint(vec)) return null;

            RaycastHit hit;
            if (!Physics.Raycast(vec + Vector3.up * 0.5f * p.resolution.y,
                -Vector3.up, out hit, p.resolution.y)) return null;

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
            return x + y * path.size_x + z * path.size_x * path.size_y;
        }
    }

    point find_grid_point(Vector3 v)
    {
        // Convert a vector to a point
        Vector3 delta = v - origin;
        int x0 = Mathf.RoundToInt(delta.x / resolution.x);
        int y0 = Mathf.RoundToInt(delta.y / resolution.y);
        int z0 = Mathf.RoundToInt(delta.z / resolution.z);

        point ret = null;
        utils.search_outward(x0, y0, z0, Mathf.Max(size_x, size_y, size_z),
            (x, y, z) =>
            {
                ret = point.create(x, y, z, this);
                return ret != null;
            });

        return ret;
    }

    List<Vector3> calculated_path;
    HashSet<point> open;
    HashSet<point> closed;
    point goal;
    path_point_constraint constraint;

    public bool complete { get { return calculated_path != null; } }
    public int length { get { return calculated_path == null ? 0 : calculated_path.Count; } }
    public Vector3 this[int i] { get { return calculated_path[i]; } }

    public delegate bool path_point_constraint(Vector3 v);

    public path(Vector3 start, Vector3 end,
        float ground_clearance = 0.5f, Vector3 resolution = default,
        path_point_constraint constraint = null)
    {
        if (resolution == default)
            resolution = Vector3.one * 0.5f;

        if (constraint == null)
            this.constraint = (v) => true; // No constraint
        else
            this.constraint = constraint;

        this.ground_clearance = Mathf.Clamp(ground_clearance, 0, resolution.y);
        this.resolution = resolution;

        // Work out the size/origin of the coordinate system
        Vector3 min = utils.min(start, end);
        Vector3 max = utils.max(start, end);
        Vector3 delta = max - min;

        // Grid must be at least size 2
        delta = utils.max(2 * resolution, delta);

        size_x = (int)(2f * delta.x / resolution.x);
        size_y = (int)(2f * delta.y / resolution.y);
        size_z = (int)(2f * delta.z / resolution.z);

        if (size_x * size_y * size_z > int.MaxValue)
        {
            Debug.Log("Pathfinding grid is too large!");
            calculated_path = new List<Vector3> { };
            return;
        }

        origin = utils.round(0.5f * (start + end) - 0.5f * new Vector3(
                size_x * resolution.x,
                size_y * resolution.y,
                size_z * resolution.z));

        // Initialize pathfinding
        var start_p = find_grid_point(start);
        goal = find_grid_point(end);

        if (start_p == null || goal == null)
        {
            calculated_path = new List<Vector3>();
            return;
        }

        open = new HashSet<point> { start_p };
        closed = new HashSet<point> { };
        start_p.fscore = start_p.heuristic(goal);
        start_p.gscore = 0;

        if (random_path == null)
            random_path = new List<point> { start_p };
    }

    public void run_pathfinding(int iterations)
    {
        if (complete) return;

        int iter = 0;
        while (open.Count > 0)
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

        if (open.Count == 0)
            calculated_path = new List<Vector3>();
    }

    List<point> random_path;

    public void run_random_pathfinding(int iterations, int target_length)
    {
        if (complete) return;

        int iter = 0;
        while (true)
        {
            if (++iter > iterations) break;
            var current = random_path[random_path.Count - 1];

            if (random_path.Count >= target_length)
            {
                calculated_path = new List<Vector3>();
                foreach (var p in random_path)
                    calculated_path.Add(p.grounding);
                return;
            }

            var ns = current.neighbours;
            point chosen_neighbour = current;

            if (random_path.Count > 1 && ns.Count > 0)
                chosen_neighbour = utils.find_to_min(ns, (n) =>
                {
                    // Walk in streight line for as long as possible
                    Vector3 direction = random_path[random_path.Count - 1].grounding
                                      - random_path[random_path.Count - 2].grounding;

                    Vector3 ndir = n.grounding - random_path[random_path.Count - 1].grounding;

                    return -Vector3.Dot(direction.normalized, ndir.normalized);
                });
            else if (ns.Count > 0)
                chosen_neighbour = ns[0];

            random_path.Add(chosen_neighbour);
        }
    }

    public void draw_gizmos()
    {
        // Draw the coordinate system range
        Gizmos.color = Color.red;
        Vector3 size = new Vector3(
            size_x * resolution.x,
            size_y * resolution.y,
            size_z * resolution.z);

        Gizmos.DrawWireCube(origin + size / 2f, size);

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
                Gizmos.DrawWireCube(p.grounding, resolution);
        }

        if (closed != null)
        {
            Gizmos.color = new Color(0, 0, 1f, 0.5f);
            foreach (var p in closed)
                Gizmos.DrawWireCube(p.grounding, resolution);
        }
    }
}