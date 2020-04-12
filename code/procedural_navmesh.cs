using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class procedural_navmesh : MonoBehaviour
{
    // Size of the navagation mesh grid
    [SerializeField]
    int _size = 32;
    public int size
    {
        get { return _size; }
        set
        {
            // Set some sensible minimum/maximum values for size
            _size = value;
            if (_size < 1) _size = 1;
            if (_size > 512) _size = 512;
        }
    }

    // The size of a single mesh point
    [SerializeField]
    float _resolution = 1f;
    public float resolution
    {
        get { return _resolution; }
        set
        {
            // Make sure other things that depend on
            // resolution stay in sensible ranges 
            _resolution = value;
            ground_clearance = ground_clearance;
        }
    }

    // Approximate height of small objects/bumps that can be walked over
    [SerializeField]
    float _ground_clearance = 0.5f;
    public float ground_clearance
    {
        get { return _ground_clearance; }
        set
        {
            _ground_clearance = value;
            if (_ground_clearance < 0) _ground_clearance = 0;
            if (_ground_clearance >= resolution) _ground_clearance = resolution;
        }
    }

    // The maximum incline that can be scaled (in degrees)
    [SerializeField]
    float _max_incline_angle = 45f;
    public float max_incline_angle
    {
        get { return _max_incline_angle; }
        set
        {
            _max_incline_angle = value;
            if (_max_incline_angle < 0) _max_incline_angle = 0;
            if (_max_incline_angle > 90) _max_incline_angle = 90;
        }
    }

    // The number of navmesh updates per frame (for spreading load across frames)
    [SerializeField]
    int _iterations_per_frame = 64;
    public int iterations_per_frame
    {
        get { return _iterations_per_frame; }
        set
        {
            _iterations_per_frame = value;
            if (_iterations_per_frame < 0) _iterations_per_frame = 0;
        }
    }

    [SerializeField]
    public bool always_draw_gizmos = false;  // True if gizmos should be drawn even if the object isn't selected

    // A particular point on the navagation mesh, optimized for use
    // with HashSets etc.
    class point
    {
        // Navagation mesh to which this point belongs
        procedural_navmesh mesh;

        // Coordinates of this point in the mesh grid
        int x; int y; int z;

        // The grounding point on walkable geometry
        public Vector3 grounding { get; private set; }

        // Equality method for HashSets etc.
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (!(obj is point)) return false;
            point p = (point)obj;
            return x == p.x && y == p.y && z == p.z;
        }

        // Hash code for HashSets etc.
        public override int GetHashCode()
        {
            return x + y * mesh.size + z * mesh.size * mesh.size;
        }

        // Either load the point from the navagation mesh, or (attempt to) 
        // create the point on the navagation mesh.
        public static point load_or_create(
            int x, int y, int z, procedural_navmesh mesh)
        {
            if (x < 0 || y < 0 || z < 0) return null;
            if (x >= mesh.size || y >= mesh.size || z >= mesh.size) return null;
            var p = mesh.grid[x, y, z];
            if (p != null) return p;

            RaycastHit hit;
            Vector3 gp = mesh.grid_point(x, y, z);
            if (!Physics.Raycast(
                gp + Vector3.up * mesh.resolution / 2f,
                -Vector3.up, out hit, 1f
            )) return null;

            if (Vector3.Angle(hit.normal, Vector3.up) > mesh.max_incline_angle) return null;

            p = new point()
            {
                x = x,
                y = y,
                z = z,
                mesh = mesh,
                grounding = hit.point
            };

            mesh.grid[x, y, z] = p;
            mesh.open_points.Add(p);
            return p;
        }

        // Returns true if I can access the neibhour at the grid point xn, yn, zn
        bool can_access_neighbour(int xn, int yn, int zn)
        {
            var a = mesh.grid_point(x, y, z) + Vector3.up * mesh.ground_clearance;
            var b = mesh.grid_point(xn, yn, zn) + Vector3.up * mesh.ground_clearance;
            if (Vector3.Angle(b - a, Vector3.up) < 90 - mesh.max_incline_angle) return false;
            return !Physics.Raycast(a, b - a, (b - a).magnitude);
        }

        // My neighbours
        HashSet<point> _neighbours = new HashSet<point>();
        public HashSet<point> neighbours() { return _neighbours; }

        public void recalculate_neighbours()
        {
            // Clear previous neighbours
            foreach (var n in _neighbours) n._neighbours.Remove(this);
            _neighbours.Clear();

            for (int n = 0; n < utils.neighbouring_dxs_3d.Length; ++n)
            {
                int xn = x + utils.neighbouring_dxs_3d[n];
                int yn = y + utils.neighbouring_dys_3d[n];
                int zn = z + utils.neighbouring_dzs_3d[n];

                // Neighbour not accessable from this point, skip
                if (!can_access_neighbour(xn, yn, zn)) continue;

                // Attempt to load or create the neighbour
                var p = point.load_or_create(xn, yn, zn, mesh);
                if (p == null) continue;

                // Ensure two-way linkage
                _neighbours.Add(p);
                p._neighbours.Add(this);
            }

            mesh.open_points.Remove(this);
            mesh.points.Add(this);
        }

        public void destroy()
        {
            // Forget neighbours
            foreach (var n in neighbours())
            {
                n._neighbours.Remove(this);
                mesh.open_points.Add(n); // Re-open neighbours for search
            }

            // Remove me from the mesh entirely
            mesh.open_points.Remove(this);
            mesh.points.Remove(this);
            mesh.grid[x, y, z] = null;
            _neighbours.Clear();
        }

        public void draw_gizmos()
        {
            foreach (var n in neighbours())
                Gizmos.DrawLine(grounding, n.grounding);
        }

        public float heuristic(point other)
        {
            // Manhattan heuristic
            return Mathf.Abs(this.x - other.x) +
                   Mathf.Abs(this.y - other.y) +
                   Mathf.Abs(this.z - other.z);
        }

        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    // The grid of points (we need to maintain a grid so that we can look up
    // points that already exist quickly). To save memory, this could potentially 
    // be removed by instead using points.TryGetValue and open_points.TryGetValue, 
    // whenever that is supported by the .net version used in unity.
    point[,,] grid;
    HashSet<point> points = new HashSet<point>();      // The points that loaded and up-to-date
    HashSet<point> open_points = new HashSet<point>(); // The points that are loaded but need updating

    // The position of the 0,0,0 point
    Vector3 grid_origin { get { return transform.position - Vector3.one * resolution * size / 2f; } }

    // The position of the x,y,z point on the grid
    Vector3 grid_point(int x, int y, int z) { return grid_origin + new Vector3(resolution * x, resolution * y, resolution * z); }

    // Inverse of the above (clamps the result to the grid)
    int[] grid_point(Vector3 world_position)
    {
        Vector3 delta = world_position - grid_origin;
        int[] ret = new int[3];
        ret[0] = Mathf.Clamp((int)(delta.x / resolution), 0, size);
        ret[1] = Mathf.Clamp((int)(delta.y / resolution), 0, size);
        ret[2] = Mathf.Clamp((int)(delta.z / resolution), 0, size);
        return ret;
    }

    // Called when an obstacle within the navmesh moves
    public void on_obstacle_move(procedural_navmesh_obstacle obstacle,
                                 Vector3 old_pos, Vector3 new_pos)
    {
        // Find all the points near to the obstacle at either the
        // old_pos or the new_pos, and remove them. Hanging neighbours
        // will automatically be sceduled for re-evaluation.
        foreach (Vector3 c in new Vector3[] { old_pos, new_pos })
        {
            int[] min = grid_point(c - obstacle.bounds.extents - Vector3.one * resolution * 2);
            int[] max = grid_point(c + obstacle.bounds.extents + Vector3.one * resolution * 2);

            for (int x = min[0]; x < max[0]; ++x)
                for (int y = min[1]; y < max[1]; ++y)
                    for (int z = min[2]; z < max[2]; ++z)
                    {
                        var p = grid[x, y, z];
                        if (p == null) continue;
                        p.destroy();
                    }
        }
    }

    point search_for_point(Vector3 v)
    {
        point p = null;
        int[] c = grid_point(v);

        // First, search downward for point
        for (int y = c[1]; y >= 0; --y)
        {
            p = point.load_or_create(c[0], y, c[2], this);
            if (p != null) return p;
        }

        // Then, search upward
        for (int y = c[1] + 1; y < size; ++y)
        {
            p = point.load_or_create(c[0], y, c[2], this);
            if (p != null) return p;
        }

        // Then search all already loaded points (if there are any)
        // for the nearest
        if (points.Count > 0)
            return utils.find_to_min(points, (pt) => (pt.grounding - v).magnitude);

        // TODO: potentially perform exaustive search?
        return null;
    }

    public List<Vector3> path(Vector3 start, Vector3 goal)
    {
        point start_p = search_for_point(start);
        point goal_p = search_for_point(goal);
        if (start_p == null || goal_p == null) return null;
        return path(start_p, goal_p);
    }

    List<Vector3> path(point start, point goal)
    {
        var open = new HashSet<point> { start };
        var came_from = new Dictionary<point, point>();
        var gscore = new Dictionary<point, float>();
        var fscore = new Dictionary<point, float>();

        gscore[start] = 0;
        fscore[start] = start.heuristic(goal);

        while (open.Count > 0)
        {
            var current = utils.find_to_min(open, (p) => fscore[p]);
            if (current.Equals(goal))
            {
                List<Vector3> ret = new List<Vector3> { current.grounding };
                while (came_from.ContainsKey(current))
                {
                    current = came_from[current];
                    ret.Add(current.grounding);
                }
                ret.Reverse();
                return ret;
            }

            open.Remove(current);

            current.recalculate_neighbours();
            foreach (var n in current.neighbours())
            {
                float tgs = gscore[current] + 1f;
                float gsn;
                if (!gscore.TryGetValue(n, out gsn)) gsn = float.PositiveInfinity;
                if (tgs < gsn)
                {
                    came_from[n] = current;
                    gscore[n] = tgs;
                    fscore[n] = tgs + n.heuristic(goal);
                    open.Add(n);
                }
            }
        }

        return null;
    }

    void Update()
    {
        // Run iterations_per_frame mesh expansions
        for (int iter = 0; iter < iterations_per_frame; ++iter)
        {
            // There are no open points left to expand
            if (open_points.Count == 0) break;

            // Just get the first point that needs updating
            point current = null;
            foreach (var op in open_points)
            {
                current = op;
                break;
            }

            // Recalculate neighbours, adding new neighbours 
            // to the open set.
            current.recalculate_neighbours();
        }
    }

    // Trigger a full regeneration of the navmesh
    void regenerate()
    {
        grid = new point[size, size, size];
        open_points.Clear();
        points.Clear();

        // Seed the navmesh around the centre point, by raycasting from the top to
        // the bottom. Note that this will generate the navmesh on the tallest piece
        // of geometry at the centre. To generate around another piece of geometry,
        // use try_seed_point.
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * size * resolution / 2f,
                        -Vector3.up, out hit, size * resolution))
        {
            for (int n = 0; n < utils.neighbouring_dxs_3d.Length; ++n)
                try_seed_point(hit.point + resolution * new Vector3(
                    utils.neighbouring_dxs_3d[n],
                    utils.neighbouring_dys_3d[n],
                    utils.neighbouring_dzs_3d[n]
                ));
        }
    }

    // Discover an open set point near v
    public void try_seed_point(Vector3 v)
    {
        if (grid == null) return;
        var c = grid_point(v);
        var p = point.load_or_create(c[0], c[1], c[2], this);
        if (p != null) open_points.Add(p);
    }

    // All navigation meshes currently Start()'ed
    public static List<procedural_navmesh> meshes = new List<procedural_navmesh>();

    void Start()
    {
        regenerate();
        meshes.Add(this); // Keep track of navmeshes
    }

    void OnDestroy()
    {
        meshes.Remove(this); // Keep track of navmeshes
    }

    // The bounding box of this navmesh
    public Bounds bounds { get { return new Bounds(transform.position, Vector3.one * size * resolution); } }

    void OnDrawGizmos()
    {
        // Draw gizmos even when not selected if always_draw_gizmos is true
        if (always_draw_gizmos)
            OnDrawGizmosSelected();
    }

    void OnDrawGizmosSelected()
    {
        // Draw the bounding box of the navmesh
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        // Draw closed points in blue
        if (points == null) return;
        Gizmos.color = Color.blue;
        foreach (var p in points)
            p.draw_gizmos();

        // Draw open points in cyan
        Gizmos.color = Color.cyan;
        foreach (var p in open_points)
            p.draw_gizmos();
    }

    // Custom inspector
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(procedural_navmesh))]
    class proc_nav_editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var nm = (procedural_navmesh)target;
            
            // Add a little button to trigger regeneration
            var regen = UnityEditor.EditorGUILayout.Toggle("Trigger regeneration", false);
            if (regen) nm.regenerate();

            nm.size = UnityEditor.EditorGUILayout.IntField("Size", nm.size);
            nm.resolution = UnityEditor.EditorGUILayout.FloatField("Resolution", nm.resolution);
            nm.ground_clearance = UnityEditor.EditorGUILayout.FloatField("Ground clearance", nm.ground_clearance);
            nm.max_incline_angle = UnityEditor.EditorGUILayout.FloatField("Max incline angle", nm.max_incline_angle);
            nm.iterations_per_frame = UnityEditor.EditorGUILayout.IntField("Iterations per frame", nm.iterations_per_frame);
            nm.always_draw_gizmos = UnityEditor.EditorGUILayout.Toggle("Always draw gizmos", nm.always_draw_gizmos);
        }
    }
#endif

}