using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class procedural_navmesh : MonoBehaviour
{
    public int size = 32;
    public float resolution = 1f;
    public float ground_clearance = 0.25f;
    public float max_incline_angle = 60f;
    public int iterations_per_frame = 64;
    public bool always_draw_gizmos = false;
    public bool pause_update = false;

    class point
    {
        int x; int y; int z;
        procedural_navmesh mesh;
        public Vector3 grounding { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (!(obj is point)) return false;
            point p = (point)obj;
            return x == p.x && y == p.y && z == p.z;
        }

        public override int GetHashCode()
        {
            return x + y * mesh.size + z * mesh.size * mesh.size;
        }

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
            return p;
        }

        bool can_access_neighbour(int xn, int yn, int zn)
        {
            var a = mesh.grid_point(x, y, z) + Vector3.up * mesh.ground_clearance;
            var b = mesh.grid_point(xn, yn, zn) + Vector3.up * mesh.ground_clearance;
            if (Vector3.Angle(b - a, Vector3.up) < 90 - mesh.max_incline_angle) return false;
            return !Physics.Raycast(a, b - a, (b - a).magnitude);
        }

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

                if (!can_access_neighbour(xn, yn, zn)) continue;

                var p = point.load_or_create(xn, yn, zn, mesh);
                if (p == null) continue;

                _neighbours.Add(p);
                p._neighbours.Add(this);
            }
        }

        public void draw_gizmos()
        {
            foreach (var n in neighbours())
                Gizmos.DrawLine(grounding, n.grounding);
        }

        public void destroy()
        {
            // Forget neighbours
            foreach (var n in neighbours())
            {
                n._neighbours.Remove(this);
                mesh.open_points.Add(n); // Re-open neighbours
            }

            // Remove me from the mesh entirely
            mesh.open_points.Remove(this);
            mesh.points.Remove(this);
            mesh.grid[x, y, z] = null;
            _neighbours.Clear();
        }
    }
    point[,,] grid;
    HashSet<point> points;
    HashSet<point> open_points;

    Vector3 grid_origin
    {
        get
        {
            return transform.position - Vector3.one * resolution * size / 2f;
        }
    }

    Vector3 grid_point(int x, int y, int z)
    {
        return grid_origin + new Vector3(resolution * x, resolution * y, resolution * z);
    }

    int[] grid_point(Vector3 world_position)
    {
        Vector3 delta = world_position - grid_origin;
        int[] ret = new int[3];
        ret[0] = Mathf.Clamp((int)(delta.x / resolution), 0, size);
        ret[1] = Mathf.Clamp((int)(delta.y / resolution), 0, size);
        ret[2] = Mathf.Clamp((int)(delta.z / resolution), 0, size);
        return ret;
    }

    public void on_obstacle_move(procedural_navmesh_obstacle obstacle,
                                 Vector3 old_pos, Vector3 new_pos)
    {
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

    void Update()
    {
        if (pause_update) return;
        for (int iter = 0; iter < iterations_per_frame; ++iter)
        {
            if (open_points == null || open_points.Count == 0) break;

            point current = null;
            foreach (var op in open_points)
            {
                current = op;
                break;
            }

            current.recalculate_neighbours();
            foreach (var n in current.neighbours())
                if (!points.Contains(n))
                    open_points.Add(n);

            open_points.Remove(current);
            points.Add(current);
        }
    }

    void regenerate()
    {
        grid = new point[size, size, size];

        var start = point.load_or_create(size / 2, size / 2, size / 2, this);
        if (start == null) return;

        open_points = new HashSet<point> { start };
        points = new HashSet<point> { };
    }

    public static List<procedural_navmesh> meshes = new List<procedural_navmesh>();

    public Bounds bounds
    {
        get
        {
            return new Bounds(transform.position, Vector3.one * size * resolution);
        }
    }

    void Start()
    {
        meshes.Add(this);
        regenerate();
    }

    void OnDrawGizmos()
    {
        if (always_draw_gizmos)
            OnDrawGizmosSelected();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (points == null) return;
        Gizmos.color = Color.blue;
        foreach (var p in points)
            p.draw_gizmos();

        Gizmos.color = Color.cyan;
        foreach (var p in open_points)
            p.draw_gizmos();
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(procedural_navmesh))]
    class proc_nav_editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var nm = (procedural_navmesh)target;
            var regen = UnityEditor.EditorGUILayout.Toggle("regenerate", false);
            if (regen) nm.regenerate();
            base.OnInspectorGUI();
        }
    }
#endif

}