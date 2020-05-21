using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class world_object : MonoBehaviour
{
    public chunk chunk { get; private set; }
    public int x_in_chunk { get; private set; }
    public int z_in_chunk { get; private set; }
    public biome.point point { get; private set; }
    public Vector3 terrain_normal { get; private set; }

    public abstract void on_placement();

    public void on_placement(Vector3 terrain_normal, biome.point point,
                             chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        this.chunk = chunk;
        this.x_in_chunk = x_in_chunk;
        this.z_in_chunk = z_in_chunk;
        this.point = point;
        this.terrain_normal = terrain_normal;
        on_placement();
    }

    public virtual bool can_place(biome.point p, Vector3 terrain_normal)
    {
        return true;
    }

    static Dictionary<string, world_object> _library;
    public static world_object load(string name)
    {
        if (_library == null)
        {
            _library = new Dictionary<string, world_object>();
            foreach (var o in Resources.LoadAll<world_object>("world_objects/"))
                _library[o.name] = o;
        }
        return _library[name];
    }

    static Dictionary<string, building_generator> _building_library;
    public static building_generator load_building(string name)
    {
        if (_building_library == null)
        {
            _building_library = new Dictionary<string, building_generator>();
            foreach (var o in Resources.LoadAll<building_generator>("buildings/"))
                _building_library[o.name] = o;
        }
        return _building_library[name];
    }
}