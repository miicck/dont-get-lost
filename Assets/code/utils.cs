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

    // Create an exact copy of the object t at the given position
    public static T inst<T>(this T t, Vector3 pos, Quaternion rot = default) where T : Object
    {
        var ret = Object.Instantiate(t, pos, rot);
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

    // i % m, but remains positive in the range [0, m)
    // such that -1 % 10 = 9 rather than -1
    public static int positive_mod(int i, int m)
    {
        int r = i % m;
        return r < 0 ? r + m : r;
    }

    // Get the screen point of the given world position for the given camera
    public static Vector3 clamped_screen_point(Camera cam, Vector3 world_position, out bool on_edge)
    {
        Vector3 sp = cam.WorldToScreenPoint(world_position);
        on_edge = false;

        // Clamp to screen in x direction
        if (sp.x > Screen.width)
        {
            on_edge = true;
            sp.x = Screen.width;
        }
        else if (sp.x < 0)
        {
            on_edge = true;
            sp.x = 0;
        }

        // Clamp to screen in y direction
        if (sp.y > Screen.height)
        {
            on_edge = true;
            sp.y = Screen.height;
        }
        else if (sp.y < 0)
        {
            on_edge = true;
            sp.y = 0;
        }

        if (sp.z < 0)
        {
            on_edge = true;

            // Behind the camera, this hack seems to look ok
            if (sp.x < Screen.width / 2) sp.x = Screen.width;
            else sp.x = 0;

            sp.y = Screen.height - sp.y;
        }

        return sp;
    }

    // Raycast for the nearest object of the given type
    public delegate bool raycast_accept_func<T>(RaycastHit h, T t);
    public static T raycast_for_closest<T>(Ray ray, out RaycastHit hit,
        float max_distance = float.MaxValue, raycast_accept_func<T> accept = null)
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
                    if (!accept(h, t))
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

    // Raycast for the nearest object of the given type
    public static List<T> raycast_for_closests<T>(Ray ray, out RaycastHit hit,
        float max_distance = float.MaxValue, raycast_accept_func<T> accept = null)
    {
        float min_dis = float.MaxValue;
        hit = new RaycastHit();
        List<T> ret = new List<T>();

        foreach (var h in Physics.RaycastAll(ray, max_distance))
        {
            var ts = h.collider.gameObject.GetComponentsInParent<T>();
            if (ts == null || ts.Length == 0) continue;

            float dis = (ray.origin - h.point).sqrMagnitude;
            if (dis < min_dis)
            {
                min_dis = dis;
                hit = h;
                ret = new List<T>();

                foreach (var t in ts)
                {
                    if (accept != null)
                        if (!accept(h, t))
                            continue;

                    ret.Add(t);
                }
            }
        }

        return ret;
    }

    /// <summary> Raycast for a <typeparamref name="T"/> under the mouse. </summary>
    public static T raycast_ui_under_mouse<T>(bool require_cursor_visible = true)
    {
        if (!Cursor.visible && require_cursor_visible) return default;

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

    /// <summary> Raycast for all <typeparamref name="T"/>s under the mouse. </summary>
    public static T[] raycast_all_ui_under_mouse<T>(bool require_cursor_visible = true)
    {
        if (!Cursor.visible && require_cursor_visible) return new T[0];

        // Setup the raycast
        var event_system = UnityEngine.EventSystems.EventSystem.current;
        var pointer_data = new UnityEngine.EventSystems.PointerEventData(event_system)
        {
            position = Input.mousePosition
        };

        var hits = new List<UnityEngine.EventSystems.RaycastResult>();

        // This never seems to work, but I guess it might as well stay
        // event_system.RaycastAll(pointer_data, hits);

        // Find the graphic raycaster and use it to find ui elements below the pointer
        var raycaster = Object.FindObjectOfType<UnityEngine.UI.GraphicRaycaster>();
        raycaster.Raycast(pointer_data, hits);

        // Find an object with the given component type
        List<T> ret = new List<T>();
        foreach (var h in hits)
        {
            var t = h.gameObject.GetComponentInChildren<T>();
            if (t != null) ret.Add(t);
        }

        return ret.ToArray();
    }

    public static T get_child_with_name<T>(this Component c, string name) where T : Component
    {
        foreach (var t in c.GetComponentsInChildren<T>())
            if (t.name == name)
                return t;
        return default;
    }


    /// <summary> Simmilar to <see cref="System.Lazy{T}"/>, but not thread 
    /// safe because we don't need that for most unity stuff. </summary>
    public class lazy<T>
    {
        public delegate T creator();
        creator create;
        public lazy(creator c) { create = c; }

        public T value
        {
            get
            {
                if (_value == null)
                    _value = create();
                return _value;
            }
        }
        T _value;
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

        float r12 = Vector3.Dot(r1, r2);
        Vector3 c = a2 - a1;

        float lambda = Vector3.Dot(c, -r2 + r1 * r12) / (1 - r12 * r12);
        return a2 + lambda * r2;
    }

    // Find the object in to_search that minimizes the given function
    public delegate float float_func<T>(T t);
    public static T find_to_min<T>(IEnumerable<T> to_search, float_func<T> objective)
    {
        T ret = default;
        if (to_search == null) return ret;
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

    public delegate bool search_func_2d(int x, int y);
    public static void search_outward_2d(int x0, int y0, int max_range, search_func_2d sf)
    {
        for (int m = 0; m <= max_range; ++m)
            for (int xm = 0; xm <= m; ++xm)
            {
                int ym = m - xm;
                for (int xs = -1; xs < 2; xs += 2)
                    for (int ys = -1; ys < 2; ys += 2)
                        if (sf(x0 + xm * xs, y0 + ym * ys))
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

    /// <summary> Move the transform <paramref name="t"/> towards the point <paramref name="to"/> by an amount
    /// bounded from above by <paramref name="max_move"/> until <paramref name="t"/> is within 
    /// <paramref name="arrive_distance"/> of <paramref name="to"/>. Returns true once this criteria is met. </summary>
    public static bool move_towards(Transform t, Vector3 to, float max_move, float arrive_distance = 0)
    {
        Vector3 delta = to - t.position;
        if (delta.magnitude < arrive_distance)
            return true;

        bool arrived = false;
        if (delta.magnitude > max_move)
            delta = delta.normalized * max_move;
        else
            arrived = true;
        t.position += delta;
        return arrived;
    }

    /// <summary> Same as <see cref="move_towards(Transform, Vector3, float, float)"/>, except the transform 
    /// will be made to look along the path. If <paramref name="level_look"/> is true, then the look vector
    /// will have it's y componenet set to zero. </summary>
    public static bool move_towards_and_look(Transform t, Vector3 to, float max_move, float arrive_distance = 0f, bool level_look = true)
    {
        Vector3 delta = to - t.position;
        if (delta.magnitude < arrive_distance)
            return true;

        Vector3 forward = delta;
        if (level_look) forward.y = 0;
        t.forward = forward;

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
        if (s == null) return s;
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

    public static T[] prepend<T>(this T[] arr, params T[] to_prepend)
    {
        var ret = new T[arr.Length + to_prepend.Length];
        for (int i = 0; i < to_prepend.Length; ++i) ret[i] = to_prepend[i];
        for (int i = to_prepend.Length; i < ret.Length; ++i) ret[i] = arr[i - to_prepend.Length];
        return ret;
    }

    public static T[] append<T>(this T[] arr, params T[] to_append)
    {
        return to_append.prepend(arr);
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

    public static float distance_to(this Component c, Component other)
    {
        return (c.transform.position - other.transform.position).magnitude;
    }

    /// <summary> Convert the given angle in degrees to the version with
    /// minimal modulus (employing negative values if neccassary). </summary>
    public static float minimal_modulus_angle(float angle)
    {
        angle -= Mathf.Floor(angle / 360f) * 360f;
        if (angle > 180) return -(360f - angle);
        return angle;
    }

    public static float random_normal(float mean, float stdDev)
    {
        float u1 = Random.Range(0, 1f);
        float u2 = Random.Range(0, 1f);
        return mean + stdDev * Mathf.Pow(-2f * Mathf.Log(u1), 0.5f) * Mathf.Cos(2 * Mathf.PI * u2);
    }

#if UNITY_EDITOR // Unity edtor utilities

    public class prefab_editor : System.IDisposable
    {
        public readonly string path;
        public readonly GameObject prefab;

        public prefab_editor(GameObject prefab)
        {
            this.prefab = prefab;
            this.path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
            this.prefab = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        }

        public void Dispose()
        {
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab, path);
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

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

public class two_way_dictionary<T, V>
{
    Dictionary<T, V> forward = new Dictionary<T, V>();
    Dictionary<V, T> backward = new Dictionary<V, T>();

    public void set(T t, V v)
    {
        removeForward(t);
        removeBackward(v);
        forward[t] = v;
        backward[v] = t;
    }

    public void removeForward(T t)
    {
        if (forward.TryGetValue(t, out V v))
        {
            forward.Remove(t);
            backward.Remove(v);
        }
    }

    public void removeBackward(V v)
    {
        if (backward.TryGetValue(v, out T t))
        {
            backward.Remove(v);
            forward.Remove(t);
        }
    }

    public bool TryGetValueForward(T t, out V v) => forward.TryGetValue(t, out v);
    public bool TryGetValueBackward(V v, out T t) => backward.TryGetValue(v, out t);
    public bool ContainsForward(T t) => forward.ContainsKey(t);
    public bool ContainsBackward(V v) => backward.ContainsKey(v);
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

class temporary_object : MonoBehaviour
{
    public static temporary_object create(float lifetime)
    {
        var to = new GameObject("temp_object").AddComponent<temporary_object>();
        to.Invoke("delete_temp_object", lifetime);
        return to;
    }

    void delete_temp_object() { Destroy(gameObject); }
}

public class kd_tree<T>
{
    class node
    {
        public T value;

        public node left_child
        {
            get => _left_child;
            set
            {
                _left_child = value;
                if (_left_child != null)
                {
                    _left_child.depth = depth + 1;
                    _left_child.parent = this;
                }
            }
        }
        node _left_child;

        public node right_child
        {
            get => _right_child;
            set
            {
                _right_child = value;
                if (_right_child != null)
                {
                    _right_child.depth = depth + 1;
                    _right_child.parent = this;
                }
            }
        }
        node _right_child;

        public int depth { get; private set; }

        public node parent { get; private set; }

        /// <summary> Searches downwards, deciding direction based on what takes 
        /// us closer to <paramref name="target"/>. Stops when <paramref name="target"/> 
        /// is found, or when a leaf is found. </summary>
        public node recurse_downward(T target, axis_difference axis_dist)
        {
            // Search downwards until we find the node that
            // target would be a child of
            node current = this;
            while (true)
            {
                if (current.value.Equals(target)) return current;
                node child = current.get_nearer_child(target, axis_dist);
                if (child == null) break;
                current = child;
            }
            return current;
        }

        public node other_child(node child)
        {
            if (left_child == child) return right_child;
            else if (right_child == child) return left_child;
            else throw new System.Exception("The given node was not a child node!");
        }

        public node get_nearer_child(T to, axis_difference axis_diff)
        {
            if (value == null) throw new System.Exception("Can't find child of invalid node!");
            return axis_diff(to, value, depth) < 0 ? left_child : right_child;
        }

        public void set_child(T child, axis_difference axis_diff)
        {
            if (axis_diff(child, value, depth) < 0)
            {
                if (left_child != null)
                    throw new System.Exception("Tried to overwrite left child of KD tree!");
                left_child = new node { value = child };
            }
            else
            {
                if (right_child != null)
                    throw new System.Exception("Tried to overwrite right child of KD tree!");
                right_child = new node { value = child };
            }
        }

        public void remove_child(node child)
        {
            if (child == right_child) right_child = null;
            else if (child == left_child) left_child = null;
            else throw new System.Exception("The given child was not a child of this node!");
        }
    }

    public delegate float axis_difference(T t1, T t2, int depth);
    public delegate float total_distance(T t1, T t2);

    node root;
    axis_difference axis_dist;

    /// <summary> Initializes a KD tree with a comparison function
    /// <paramref name="axis_dist"/>(a,b,d) which returns
    /// the difference b-a along axis d of the hyperspace. </summary>
    public kd_tree(axis_difference axis_dist)
    {
        this.axis_dist = axis_dist;
    }

    /// <summary> Adds the given node to the tree. </summary>
    public void add(T t)
    {
        // This is the first, and hence root, node
        if (root == null)
        {
            root = new node { value = t };
            return;
        }

        // Search downwards until we find the node that
        // either contains t, or should have t has a child
        var found = root.recurse_downward(t, axis_dist);
        if (found.value.Equals(t)) return; // t already in tree
        found.set_child(t, axis_dist); // Add as child of leaf
    }

    /// <summary> Remove a node from the kd tree, returns true
    /// if the node was successfully removed. </summary>
    public bool remove(T t)
    {
        // There is no tree
        if (root == null)
            return false;

        node current = root;
        while (true)
        {
            if (current.value.Equals(t))
            {
                // We've found the location of t in the tree. Removing
                // it will break the tree from here down, so we need to
                // re-add everyhting below this point
                if (current == root) root = null; // Special case where t was at the top
                else current.parent.remove_child(current);

                // Accumulate all children of current
                List<T> children = new List<T>();

                // Start with a queue containing just the current node
                var to_add_children_of = new Queue<node>();
                to_add_children_of.Enqueue(current);

                // Recure down the tree of all non-null child nodes
                // adding all non-null values to children
                while (to_add_children_of.Count > 0)
                {
                    var dq = to_add_children_of.Dequeue();
                    if (dq.left_child != null)
                    {
                        to_add_children_of.Enqueue(dq.left_child);
                        if (dq.left_child.value != null)
                            children.Add(dq.left_child.value);
                    }

                    if (dq.right_child != null)
                    {
                        to_add_children_of.Enqueue(dq.right_child);
                        if (dq.right_child.value != null)
                            children.Add(dq.right_child.value);
                    }
                }

                // Reconstruct the broken tree
                foreach (var c in children) add(c);
                return true;
            }

            // Move down the tree
            current = current.get_nearer_child(t, axis_dist);

            // We've reached the bottom of the tree, but 
            // havent found t => it wasn't in the tree
            if (current == null)
                break;
        }

        return false;
    }

    /// <summary> Returns the nearest neighbour in the tree to 
    /// <paramref name="target"/> according to <paramref name="f"/>. </summary>
    public T nearest_neighbour(T target, total_distance f)
    {
        float best_dist = Mathf.Infinity;
        node best_node = null;

        // Start at the node where target would be put
        node current = root.recurse_downward(target, axis_dist);
        node child_came_from = null;

        // Recurse upwards searching for any better nodes, or nodes 
        // where we could have gone a different way
        while (current != null)
        {
            // See if this node is closer to the 
            // target than the current best
            float dis = Mathf.Abs(f(current.value, target));
            if (dis < best_dist)
            {
                best_dist = dis;
                best_node = current;
            };

            if (child_came_from != null)
            {
                // See if we need to search the other side of this node from where we came from
                node other_child = current.other_child(child_came_from);
                if (other_child != null && Mathf.Abs(axis_dist(current.value, target, current.depth)) < best_dist)
                {
                    // We do, search down the tree from the other child
                    current = other_child.recurse_downward(target, axis_dist);
                    child_came_from = null;
                }
            }

            child_came_from = current;
            current = current.parent;
        }

        return best_node.value;
    }

    public static void test2()
    {
        // Setup a kd tree of integers
        var tree = new kd_tree<int>((a, b, d) => b - a);
        tree.add(0);
        tree.add(-2);
        tree.add(-1);
        tree.add(-3);
        tree.add(1);
        tree.add(1);
        tree.remove(-1);

        string str = "";
        var to_print = new Queue<kd_tree<int>.node>();
        to_print.Enqueue(tree.root);
        int last_depth = tree.root.depth;

        while (to_print.Count > 0)
        {
            var dq = to_print.Dequeue();

            if (dq.depth > last_depth)
            {
                last_depth = dq.depth;
                str += "\n";
            }

            str += dq.value + " ";
            if (dq.left_child != null) to_print.Enqueue(dq.left_child);
            if (dq.right_child != null) to_print.Enqueue(dq.right_child);
        }

        Debug.Log(str);
        Debug.Log(tree.nearest_neighbour(-4, (a, b) => a - b));
    }

    public static void test()
    {
        var tree = new kd_tree<Vector3>((a, b, d) =>
        {
            switch (d % 3)
            {
                case 0: return b.x - a.x;
                case 1: return b.y - a.y;
                case 2: return b.z - a.z;
                default: throw new System.Exception("Unkown axis!");
            }
        });

        for (int n = 0; n < 100; ++n)
            tree.add(Random.insideUnitSphere);

        Debug.Log(tree.nearest_neighbour(Vector3.zero, (a, b) => (a - b).magnitude));
    }
}