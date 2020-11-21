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

    /// <summary> Raycast for a <typeparamref name="T"/> under the mouse. </summary>
    public static T raycast_ui_under_mouse<T>()
    {
        // Setup the raycast
        var event_system = UnityEngine.EventSystems.EventSystem.current;
        var pointer_data = new UnityEngine.EventSystems.PointerEventData(event_system)
        {
            position = Input.mousePosition
        };

        var hits = new List<UnityEngine.EventSystems.RaycastResult>();

        // This never seems to work, but I guess it might as well stay
        event_system.RaycastAll(pointer_data, hits);

        // Find the graphic raycaster and use it to find ui elements below the pointer
        var raycaster = Object.FindObjectOfType<UnityEngine.UI.GraphicRaycaster>();
        raycaster.Raycast(pointer_data, hits);

        // Find an object with the given component type
        foreach (var h in hits)
        {
            var t = h.gameObject.GetComponentInChildren<T>();
            if (t != null) return t;
        }

        return default;
    }

    /// <summary> Returns the point on the given <paramref name="world_line"/>, that 
    /// passes closest to the players camera ray. </summary>
    public static Vector3 nearest_point_on_line_to_player_ray(Ray world_line)
    {
        var cam_ray = player.current.camera_ray();

        Vector3 a1 = cam_ray.origin;
        Vector3 r1 = cam_ray.direction;
        Vector3 a2 = world_line.origin;
        Vector3 r2 = world_line.direction;

        a1_last = a1;
        a2_last = a2;
        r1_last = r1;
        r2_last = r2;

        float r12 = Vector3.Dot(r1, r2);
        Vector3 c = a2 - a1;

        float lambda = Vector3.Dot(c, -r2 + r1 * r12) / (1 - r12 * r12);
        return a2 + lambda * r2;
    }

    static Vector3 a1_last;
    static Vector3 a2_last;
    static Vector3 r1_last;
    static Vector3 r2_last;

    public static void draw_gizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(a1_last, a1_last + r1_last);
        Gizmos.DrawLine(a2_last, a2_last + r2_last);
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

    /// <summary> Conver an integer into a quantity string. </summary>
    public static string qs(this int i)
    {
        return int_to_quantity_string(i);
    }

    public static string int_to_comma_string(int i)
    {
        string str = "" + i;
        List<char> chars = new List<char>();
        int count = 0;
        for (int n = str.Length - 1; n >= 0; --n)
        {
            count++;
            chars.Add(str[n]);

            if (count % 3 == 0 && n != 0)
                chars.Add(',');
        }
        chars.Reverse();
        return new string(chars.ToArray());
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

    /// <summary> Allign all of the :'s (preceded by a space) on each line of s. </summary>
    public static string allign_colons(string s)
    {
        // Allign all of the :'s precceded by a space
        int max_found = 0;
        foreach (var line in s.Split('\n'))
        {
            int found = line.IndexOf(':');
            if (found > max_found)
            {
                if (line[found - 1] != ' ') continue;
                max_found = found;
            }
        }

        string padded = "";
        foreach (var line in s.Split('\n'))
        {
            int found = line.IndexOf(':');
            string padded_line = line;
            if (found > 0)
            {
                padded_line = line.Substring(0, found);
                for (int i = 0; i < max_found - found; ++i)
                    padded_line += " ";
                padded_line += line.Substring(found);
            }
            padded += padded_line + "\n";
        }

        return padded;
    }

    /// <summary> Returns true if the dictionaries <paramref name="a"/> 
    /// and <paramref name="b"/> contain the same key-value pairs. </summary>
    public static bool compare_dictionaries<T, K>(Dictionary<T, K> a, Dictionary<T, K> b)
    {
        // Check all the keys in a are in b
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out K val)) return false;
            if (!kv.Value.Equals(val)) return false;
        }

        // Check all the keys in b are in a
        foreach (var kv in b)
        {
            if (!a.TryGetValue(kv.Key, out K val)) return false;
            if (!kv.Value.Equals(val)) return false;
        }

        return true;
    }

    public static Vector3 clamp_magnitude(this Vector3 v, float min_mag, float max_mag)
    {
        if (v.magnitude < min_mag) v = v.normalized * min_mag;
        if (v.magnitude > max_mag) v = v.normalized * max_mag;
        return v;
    }

    public static bool move_towards(Transform t, Vector3 to, float max_move)
    {
        Vector3 delta = to - t.position;
        bool arrived = false;
        if (delta.magnitude > max_move)
            delta = delta.normalized * max_move;
        else
            arrived = true;
        t.position += delta;
        return arrived;
    }

    public static bool rotate_towards(Transform t, Quaternion to, float max_angle)
    {
        t.rotation = Quaternion.RotateTowards(t.rotation, to, max_angle);
        return Quaternion.Angle(t.rotation, to) < 0.1f;
    }

    public static Vector3 axis(this Transform t, AXIS axis)
    {
        switch (axis)
        {
            case AXIS.X_AXIS: return t.right;
            case AXIS.Y_AXIS: return t.up;
            case AXIS.Z_AXIS: return t.forward;
            default: throw new System.Exception("Unkown axis: " + axis);
        }
    }

    public static void set_axis(this Transform t, AXIS axis, Vector3 val)
    {
        switch (axis)
        {
            case AXIS.X_AXIS:
                t.right = val;
                break;

            case AXIS.Y_AXIS:
                t.up = val;
                break;

            case AXIS.Z_AXIS:
                t.forward = val;
                break;

            default: throw new System.Exception("Unkown axis: " + axis);
        }
    }

    public static string remove_special_characters(this string s, params char[] allow)
    {
        List<char> allow_list = new List<char>(allow);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in s)
            if ((c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                c == '.' || c == '_' || allow_list.Contains(c))
                sb.Append(c);
        return sb.ToString();
    }

    public static float xz_angle(Vector3 look_direction)
    {
        const float DTR = 180 / Mathf.PI;

        look_direction.Normalize();
        float x = look_direction.x;
        float z = look_direction.z;

        if (x > 0 && z > 0)
        {
            if (x < 10e-3) return 90;
            return Mathf.Atan(z / x) * DTR;
        }
        else if (x < 0 && z > 0)
        {
            if (z < 10e-3) return 180;
            return 90 - Mathf.Atan(x / z) * DTR;
        }
        else if (x < 0 && z < 0)
        {
            if (-x < 10e-3) return 270;
            return 180 + Mathf.Atan(z / x) * DTR;
        }
        else
        {
            if (-z < 10e-3) return 360;
            return 270 - Mathf.Atan(x / z) * DTR;
        }
    }

    /// <summary> Convert the given angle in degrees to the version with
    /// minimal modulus (employing negative values if neccassary). </summary>
    public static float minimal_modulus_angle(float angle)
    {
        angle -= Mathf.Floor(angle / 360f) * 360f;
        if (angle > 180) return -(360f - angle);
        return angle;
    }

#if UNITY_EDITOR // Unity edtor utilities

    public static T select_from_resources_folder<T>(string folder) where T : Object
    {
        var selected = UnityEditor.EditorUtility.OpenFilePanel("Select " + typeof(T).Name,
            Application.dataPath + "/resources/" + folder, "prefab");

        selected = System.IO.Path.GetFileName(selected).Replace(".prefab", "");
        return Resources.Load<T>(folder + "/" + selected);
    }

    public static T select_from_folder_dropdown<T>(string label, string folder, T selected) where T : Object
    {
        var options = Resources.LoadAll<T>(folder);

        int index = 0;
        for (int i = 0; i < options.Length; ++i)
            if (options[i] == selected)
            {
                index = i;
                break;
            }

        List<string> option_names = new List<string>();
        foreach (var o in options)
            option_names.Add(o.name);

        var new_index = UnityEditor.EditorGUILayout.Popup(
            label, index, option_names.ToArray());
        return options[new_index];
    }

#endif // UNITY_EDITOR
}

//####################################//
// END UTILS - BEGIN TYPE DEFINITIONS //
//####################################//

public enum AXIS
{
    X_AXIS,
    Y_AXIS,
    Z_AXIS
}

/// <summary> A dictionary with two keys. </summary>
public class Dictionary<K1, K2, V>
{
    // The underlying datastructure is just a dictionary
    Dictionary<K1, Dictionary<K2, V>> dict =
        new Dictionary<K1, Dictionary<K2, V>>();

    public int count
    {
        get
        {
            int c = 0;
            foreach (var kv in dict)
                c += kv.Value.Count;
            return c;
        }
    }

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

    public int count
    {
        get
        {
            int count = 0;
            foreach (var kv in dict)
                count += kv.Value.count;
            return count;
        }
    }

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

public class int_rect
{
    public int_rect(int left, int right, int bottom, int top)
    {
        this.left = left;
        this.right = right;
        this.bottom = bottom;
        this.top = top;
    }

    public int left { get; protected set; }
    public int bottom { get; protected set; }
    public int right { get; protected set; }
    public int top { get; protected set; }
    public int width { get => right - left; }
    public int height { get => top - bottom; }
    public int centre_x { get => (right + left) / 2; }
    public int centre_z { get => (top + bottom) / 2; }

    public bool is_edge(int edge_width, int x, int z)
    {
        return x > right - edge_width ||
               x < left + edge_width ||
               z > top - edge_width ||
               z < bottom + edge_width;
    }
}
