using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class path
{
    // We use a coordinate system specific to this
    // path instance, so that we can hash the coordinates
    // within that system.
    public const int MAX_RANGE = 1024;
    int xo; int yo; int zo;

    static bool in_range(int x) { return x >= 0 && x < MAX_RANGE; }

    // A point on the aformentioned coordinate system
    class waypoint
    {
        int x; int y; int z;

        public waypoint came_from = null;
        public float gscore = float.PositiveInfinity;
        public float fscore = float.PositiveInfinity;
        public Vector3 grounding_point { get; private set; }
        public bool valid { get; private set; }

        path path;

        public waypoint(Vector3 v, path p) : this(
            Mathf.RoundToInt(v.x) - p.xo,
            Mathf.RoundToInt(v.y) - p.yo,
            Mathf.RoundToInt(v.z) - p.zo, p
            )
        { }

        public waypoint(int x, int y, int z, path p)
        {
            this.x = x; this.y = y; this.z = z;
            if (!in_range(x) || !in_range(y) || !in_range(z))
                Debug.LogError("Waypoint out of range!");

            path = p;
            Vector3 ray_start = new Vector3(x + p.xo, y + p.yo + 0.5f, z + p.zo);
            RaycastHit hit;

            if (Physics.Raycast(ray_start, -Vector3.up, out hit, 1f))
            {
                grounding_point = hit.point;
                valid = true;
            }
            else valid = false;
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

                        var w = new waypoint(x + dx, y + dy, z + dz, path);
                        if (!w.valid) continue;
                        if (!can_path_to(w)) continue;

                        ret.Add(w);
                    }

            return ret;
        }

        bool can_path_to(waypoint neighbour)
        {
            Vector3 a = grounding_point + Vector3.up * 0.1f;
            Vector3 b = neighbour.grounding_point + Vector3.up * 0.1f;
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
            return x + y * MAX_RANGE + z * MAX_RANGE * MAX_RANGE;
        }

        public void draw_gizmos()
        {
            Gizmos.color = Color.red;
            if (valid) Gizmos.color = Color.green;
            Vector3 centre = new Vector3(
                path.xo + x,
                path.yo + y,
                path.zo + z
            );
            Gizmos.DrawWireCube(centre, Vector3.one);
        }
    }

    public path(Vector3 start, Vector3 end)
    {
        // Set the origin of the coordinate system so that the
        // centre of start, end is in the centre of the coordinate range
        Vector3 average = (start + end) / 2f;
        xo = Mathf.RoundToInt(average.x) - MAX_RANGE / 2;
        yo = Mathf.RoundToInt(average.y) - MAX_RANGE / 2;
        zo = Mathf.RoundToInt(average.z) - MAX_RANGE / 2;

        get_path(new waypoint(start, this), new waypoint(end, this));
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

    public void draw_waypoint_gizmos(Vector3 v)
    {
        var w = new waypoint(v, this);
        w.draw_gizmos();
    }

    public void draw_gizmos()
    {
        if (calculated_path == null) return;
        for (int i = 1; i < calculated_path.Count; ++i)
            Gizmos.DrawLine(calculated_path[i].grounding_point,
                            calculated_path[i - 1].grounding_point);
    }
}