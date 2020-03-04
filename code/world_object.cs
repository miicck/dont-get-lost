using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object : MonoBehaviour
{
    // Variables specifying how this world object is placed
    public bool align_with_terrain_normal = false;
    public bool random_y_rotation = true;
    public float min_scale = 0.5f;
    public float max_scale = 2.0f;
    public float min_altitude = world.SEA_LEVEL;
    public float max_altitude = world.MAX_ALTITUDE;
    public float max_terrain_angle = 90f;

    // Get my id as as it appears in the world_object library
    public int id()
    {
        return int.Parse(name.Substring(0, name.IndexOf('_')));
    }

    // Get my name as it appears in the world_object library
    public string get_name()
    {
        return name.Substring(name.IndexOf('_') + 1);
    }

    // Serialize the current state of the world_object into bytes
    public byte[] serialize()
    {
        int[] ints = new int[]
        {
            id()
        };

        float[] floats = new float[]
        {
            transform.localScale.x,
            transform.rotation.eulerAngles.y
        };

        int int_bytes = ints.Length * sizeof(int);
        int float_bytes = floats.Length * sizeof(float);

        byte[] bytes = new byte[int_bytes + float_bytes];
        System.Buffer.BlockCopy(ints, 0, bytes, 0, int_bytes);
        System.Buffer.BlockCopy(floats, 0, bytes, int_bytes, float_bytes);
        return bytes;
    }

    // This world object is ungenerated, but needs saving. Create a special
    // serialization that will be interpreted as "generation required" when loaded.
    // This serialization is chosen to be the same length as a real serialization
    // so that the serialization length is constant.
    public byte[] serialize_generate_required()
    {
        int[] ints = new int[]
        {
            id()
        };

        int int_bytes = ints.Length * sizeof(int);
        int float_bytes = 2 * sizeof(float);

        // All zeros apart from id => generation required
        byte[] bytes = new byte[int_bytes + float_bytes];
        System.Buffer.BlockCopy(ints, 0, bytes, 0, int_bytes);
        return bytes;
    }

    public static bool generation_required(byte[] serialization)
    {
        // Check for all zeros apart from id
        for (int i = sizeof(int); i < serialization.Length; ++i)
            if (serialization[i] > 0)
                return false;
        return true;
    }

    // Create a world_object from saved bytes
    public static world_object deserialize(byte[] bytes)
    {
        int id = System.BitConverter.ToInt32(bytes, 0);
        float scale = System.BitConverter.ToSingle(bytes, sizeof(int));
        float y_rot = System.BitConverter.ToSingle(bytes, sizeof(int) + sizeof(float));

        var wo = look_up(id).inst();
        wo.transform.localScale = Vector3.one * scale;
        wo.transform.Rotate(0, y_rot, 0);
        return wo;
    }

    // Get the length of a serailized world object in bytes
    public static int serialize_length()
    {
        load_library();
        return first_world_object.serialize().Length;
    }

    static Dictionary<int, world_object> int_library;
    static Dictionary<string, world_object> name_library;
    static Dictionary<string, int> string_to_int_library;
    static Dictionary<int, string> int_to_string_library;
    static world_object first_world_object;

    static void load_library()
    {
        int_library = new Dictionary<int, world_object>();
        name_library = new Dictionary<string, world_object>();
        string_to_int_library = new Dictionary<string, int>();
        int_to_string_library = new Dictionary<int, string>();

        foreach (var wo in Resources.LoadAll<world_object>("world_objects"))
        {
            if (first_world_object == null)
                first_world_object = wo;
            int loc = wo.name.IndexOf('_');
            int id = int.Parse(wo.name.Substring(0, loc));
            string name = wo.name.Substring(loc + 1);
            int_library[id] = wo;
            name_library[name] = wo;
            string_to_int_library[name] = id;
            int_to_string_library[id] = name;
        }
    }

    public static world_object look_up(int id)
    {
        if (id <= 0) return null;
        if (int_library == null) load_library();
        world_object wo;
        if (!int_library.TryGetValue(id, out wo))
            Debug.LogError("Could not load world object with id " + id);
        return wo;
    }

    public static world_object look_up(string name)
    {
        if (name == null) return null;
        if (name_library == null) load_library();
        world_object wo;
        if (!name_library.TryGetValue(name, out wo))
            Debug.LogError("Could not load world object with name " + name);
        return wo;
    }

    public static int get_id(string name)
    {
        if (name == null) return 0;
        if (string_to_int_library == null) load_library();
        int ret = 0;
        if (!string_to_int_library.TryGetValue(name, out ret))
            Debug.LogError("Could not get the id of world object with name " + name);
        return ret;
    }

    public static string get_name(int id)
    {
        if (id <= 0) return null;
        if (int_to_string_library == null) load_library();
        string ret;
        if (!int_to_string_library.TryGetValue(id, out ret))
            Debug.LogError("Could not get the name of world object with id " + id);
        return ret;
    }

    public bool can_place(biome.point p, Vector3 terrain_normal)
    {
        if (p.altitude > max_altitude) return false;
        if (p.altitude < min_altitude) return false;
        if (Vector3.Angle(terrain_normal, Vector3.up) > max_terrain_angle) return false;
        return true;
    }

    public void on_placement(Vector3 terrain_normal)
    {
        if (align_with_terrain_normal) transform.up = terrain_normal;
    }

    public void on_generation()
    {
        transform.localScale = Vector3.one * Random.Range(min_scale, max_scale);
        if (random_y_rotation) transform.Rotate(0, Random.Range(0, 360f), 0);
    }
}