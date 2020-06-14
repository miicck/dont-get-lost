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
        if (!server.started) return; // Only log on server
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

    public static float tanh(float x)
    {
        return (Mathf.Exp(x) - Mathf.Exp(-x)) / (Mathf.Exp(x) + Mathf.Exp(-x));
    }

    public static float interpolate_constant_speed(float a, float b, float speed)
    {
        float delta = speed * Time.deltaTime;
        if (Mathf.Abs(b - a) < delta) return b;
        return a + delta * Mathf.Sign(b - a);
    }

    public static Color interpolate_constant_speed(Color a, Color b, float speed)
    {
        return new Color(
            interpolate_constant_speed(a.r, b.r, speed),
            interpolate_constant_speed(a.g, b.g, speed),
            interpolate_constant_speed(a.b, b.b, speed),
            interpolate_constant_speed(a.a, b.a, speed)
        );
    }

    public static string base_color_string(Material m)
    {
        switch (m.shader.name)
        {
            case "HDRP/Unlit": return "_UnlitColor";
            case "HDRP/Lit": return "_BaseColor";
            default:
                throw new System.Exception("Unkown shader name " + m.shader.name);
        }
    }

    public static void set_color(Material m, Color c)
    {
        m.SetColor(base_color_string(m), c);
    }

    public static Color get_color(Material m)
    {
        return m.GetColor(base_color_string(m));
    }

    public static bool isNaN(this Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
    }

    /// <summary> Allign the axes of a transform to a given rotation,
    /// without affecting that transforms children. </summary>
    public static void align_axes(Transform t, Quaternion rot)
    {
        // Unparent all children of t
        List<Transform> children = new List<Transform>();
        foreach (Transform c in t) children.Add(c);
        foreach (var c in children) c.SetParent(null);

        // Rotate t to the given alignment
        Quaternion drot = rot * Quaternion.Inverse(t.rotation);
        t.rotation = drot * t.rotation;

        // Reparent all children of t
        foreach (Transform c in children) c.SetParent(t);
    }

    public static string a_or_an(string name)
    {
        string n = name.Trim().ToLower();
        switch (n[0])
        {
            case 'a':
            case 'e':
            case 'i':
            case 'o':
            case 'u':
                return "an";
            default:
                return "a";
        }
    }

    public static void gizmos_tube(Vector3 start, Vector3 end, float width)
    {
        Vector3 up = (start - end).normalized;
        Vector3 fw = new Vector3(up.y, -up.x, 0).normalized;
        float dist = (start - end).magnitude;
        Gizmos.matrix = Matrix4x4.TRS(start, Quaternion.LookRotation(fw, up), Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0, -dist / 2f, 0), new Vector3(width, dist, width));
        Gizmos.matrix = Matrix4x4.identity;
    }
}

/// <summary> A dictionary with two keys. </summary>
public class Dictionary<K1, K2, V>
{
    // The underlying datastructure is just a dictionary
    Dictionary<K1, Dictionary<K2, V>> dict =
        new Dictionary<K1, Dictionary<K2, V>>();

    /// <summary> Set the value <paramref name="v"/> associated with
    /// the keys <paramref name="k1"/> and <paramref name="k2"/>. </summary>
    public void set(K1 k1, K2 k2, V v)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, V> inner))
        {
            inner = new Dictionary<K2, V>();
            dict[k1] = inner;
        }

        inner[k2] = v;
    }

    /// <summary> Get the value <paramref name="v"/> associated with
    /// the keys <paramref name="k1"/> and <paramref name="k2"/>. </summary>
    public V get(K1 k1, K2 k2)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, V> inner))
        {
            inner = new Dictionary<K2, V>();
            dict[k1] = inner;
        }

        if (inner.TryGetValue(k2, out V ret))
            return ret;

        return default;
    }

    /// <summary> Clear the value <paramref name="v"/> associated with
    /// the keys <paramref name="k1"/> and <paramref name="k2"/>. </summary>
    public void clear(K1 k1, K2 k2)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, V> inner))
            return;

        inner.Remove(k2);
        if (inner.Count == 0)
            dict.Remove(k1);
    }

    public delegate void iter_func(K1 k1, K2 k2, V v);

    /// <summary> Apply <paramref name="f"/> to every 
    /// key-value set in the dictionary.
    public void iterate(iter_func f)
    {
        foreach (var k1v in dict)
            foreach (var k2v in k1v.Value)
            {
                K1 k1 = k1v.Key;
                K2 k2 = k2v.Key;
                V v = k2v.Value;
                f(k1, k2, v);
            }
    }
}

/// <summary> A dictionary with three keys. </summary>
public class Dictionary<K1, K2, K3, V>
{
    // The underlying datastructure is a two-key dictionary
    Dictionary<K1, Dictionary<K2, K3, V>> dict =
        new Dictionary<K1, Dictionary<K2, K3, V>>();

    /// <summary> Set the value <paramref name="v"/> 
    /// associated with the keys <paramref name="k1"/>, 
    /// <paramref name="k2"/> and <paramref name="k3"/>. </summary>
    public void set(K1 k1, K2 k2, K3 k3, V v)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, K3, V> inner))
        {
            inner = new Dictionary<K2, K3, V>();
            dict[k1] = inner;
        }

        inner.set(k2, k3, v);
    }

    /// <summary> Get the value <paramref name="v"/> 
    /// associated with the keys <paramref name="k1"/>, 
    /// <paramref name="k2"/> and <paramref name="k3"/>. </summary>
    public V get(K1 k1, K2 k2, K3 k3)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, K3, V> inner))
        {
            inner = new Dictionary<K2, K3, V>();
            dict[k1] = inner;
        }

        return inner.get(k2, k3);
    }

    /// <summary> Clear the value <paramref name="v"/> 
    /// associated with the keys <paramref name="k1"/>, 
    /// <paramref name="k2"/> and <paramref name="k3"/>. </summary>
    public void clear(K1 k1, K2 k2, K3 k3)
    {
        if (!dict.TryGetValue(k1, out Dictionary<K2, K3, V> inner))
            return;

        inner.clear(k2, k3);
    }

    public delegate void iter_func(K1 k1, K2 k2, K3 k3, V v);

    /// <summary> Apply <paramref name="f"/> to every 
    /// key-value set in the dictionary.
    public void iterate(iter_func f)
    {
        foreach (var k1v in dict)
        {
            K1 k1 = k1v.Key;
            k1v.Value.iterate((k2, k3, v) => f(k1, k2, k3, v));
        }
    }
}