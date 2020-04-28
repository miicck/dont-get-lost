using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class utils
{
    // Create an exact copy of the object t
    public static T inst<T>(this T t) where T : Object
    {
        var ret = Object.Instantiate(t);
        ret.name = t.name;
        return ret;
    }

    // Returns true if i is within size range
    public static bool in_range(int i, int size)
    {
        return (i >= 0) && (i < size);
    }

    // Rounds a float so 0.5 -> 1.0
    public static int round(float f)
    {
        int ret = Mathf.FloorToInt(f);
        f -= ret;
        if (f < 0.5f) return ret;
        return ret + 1;
    }

    // Get the sign of f (returning 0 if f is 0)
    public static int sign(float f)
    {
        if (f < 0) return -1;
        if (f > 0) return 1;
        return 0;
    }

    // Log something to file
    static HashSet<string> opened = new HashSet<string>();
    public static void log(string s, string logfile)
    {
        if (!networked.server.started) return; // Only log on server
        string filename = Application.persistentDataPath + "/" + logfile + ".log";

        if (!opened.Contains(filename))
        {
            System.IO.File.Delete(filename);
            opened.Add(filename);
        }

        using (var stream = System.IO.File.AppendText(filename))
        {
            stream.Write(s + "\n");
            stream.Flush();
        }
    }

    // Raycast for the nearest object of the given type
    public delegate bool accept_func<T>(T t);
    public static T raycast_for_closest<T>(Ray ray, out RaycastHit hit,
        float max_distance = float.MaxValue, accept_func<T> accept = null)
        where T : Component
    {
        float min_dis = float.MaxValue;
        hit = new RaycastHit();
        T ret = default;

        foreach (var h in Physics.RaycastAll(ray, max_distance))
        {
            var t = h.collider.gameObject.GetComponentInParent<T>();
            if (t != null)
            {
                if (accept != null)
                    if (!accept(t))
                        continue;

                float dis = (ray.origin - h.point).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    hit = h;
                    ret = t;
                }
            }
        }

        return ret;
    }

    // Find the object in to_search that minimizes the given function
    public delegate float float_func<T>(T t);
    public static T find_to_min<T>(IEnumerable<T> to_search, float_func<T> objective)
    {
        T ret = default;
        float min = float.PositiveInfinity;
        foreach (var t in to_search)
        {
            float val = objective(t);
            if (val < min)
            {
                min = val;
                ret = t;
            }
        }
        return ret;
    }

    // Check if the given circle intersects the given square
    public static bool circle_intersects_square(
        Vector2 circle_centre, float radius,
        Vector2 square_centre, float sq_width, float sq_height)
    {
        float dx = Mathf.Abs(circle_centre.x - square_centre.x);
        float dy = Mathf.Abs(circle_centre.y - square_centre.y);

        if (dx > sq_width / 2 + radius) return false;
        if (dy > sq_height / 2 + radius) return false;

        if (dx < sq_width / 2) return true;
        if (dy < sq_height / 2) return true;

        float corner_distance_sq = (dx - sq_width / 2) * (dx - sq_width / 2) +
                                   (dy - sq_height / 2) * (dy - sq_height / 2);

        return corner_distance_sq < radius * radius;
    }

    public static readonly int[] neighbouring_dxs_3d = new int[]
    { 1,1,1,0,0,0,-1,-1,-1,1,1,1,0,0,-1,-1,-1,1,1,1,0,0,0,-1,-1,-1};

    public static readonly int[] neighbouring_dys_3d = new int[]
    { 1,1,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,-1,-1,-1,-1,-1,-1,-1,-1,-1};

    public static readonly int[] neighbouring_dzs_3d = new int[]
    { 1,0,-1,1,0,-1,1,0,-1,1,0,-1,1,-1,1,0,-1,1,0,-1,1,0,-1,1,0,-1};

    public static Vector3 min(params Vector3[] vs)
    {
        float min_x = float.PositiveInfinity;
        float min_y = float.PositiveInfinity;
        float min_z = float.PositiveInfinity;
        for (int i = 0; i < vs.Length; ++i)
        {
            var v = vs[i];
            if (v.x < min_x) min_x = v.x;
            if (v.y < min_y) min_y = v.y;
            if (v.z < min_z) min_z = v.z;
        }
        return new Vector3(min_x, min_y, min_z);
    }

    public static Vector3 max(params Vector3[] vs)
    {
        float max_x = float.NegativeInfinity;
        float max_y = float.NegativeInfinity;
        float max_z = float.NegativeInfinity;
        for (int i = 0; i < vs.Length; ++i)
        {
            var v = vs[i];
            if (v.x > max_x) max_x = v.x;
            if (v.y > max_y) max_y = v.y;
            if (v.z > max_z) max_z = v.z;
        }
        return new Vector3(max_x, max_y, max_z);
    }

    public static Vector3 round(Vector3 v)
    {
        return new Vector3(
            Mathf.Round(v.x),
            Mathf.Round(v.y),
            Mathf.Round(v.z)
        );
    }

    public delegate bool search_func(int x, int y, int z);
    public static void search_outward(int x0, int y0, int z0, int max_range, search_func sf)
    {
        // Loop over magnitues m and xm, ym, zm 
        // such that xm+ym+zm = m and m <= max_range
        for (int m = 0; m <= max_range; ++m)
            for (int xm = 0; xm <= m; ++xm)
                for (int ym = 0; ym <= m - xm; ++ym)
                {
                    int zm = m - ym - xm;

                    // Search all combinations of x, y and z signs
                    for (int xs = -1; xs < 2; xs += 2)
                        for (int ys = -1; ys < 2; ys += 2)
                            for (int zs = -1; zs < 2; zs += 2)
                                if (sf(x0 + xm * xs, y0 + ym * ys, z0 + zm * zs))
                                    return;
                }
    }

    public static string int_to_quantity_string(int i)
    {
        // Return a runescape-style quantity
        // 2,300 -> 2.3K
        // 2,300,000 -> 2.3M
        if (i < 1000) return "" + i;
        if (i < 1000000)
        {
            float thsds = i / 1000f;
            return "" + System.Math.Round(thsds, 2) + "K";
        }
        float mils = i / 1000000f;
        return "" + System.Math.Round(mils, 2) + "M";
    }

    public static string capitalize(this string s)
    {
        if (s.Length == 0) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
}