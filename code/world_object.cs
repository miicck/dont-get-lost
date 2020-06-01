﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object generated by the world generator. </summary>
public abstract class world_object : MonoBehaviour
{
    public chunk chunk { get; private set; }
    public int x_in_chunk { get; private set; }
    public int z_in_chunk { get; private set; }
    public biome.point point { get; private set; }
    public Vector3 terrain_normal { get; private set; }

    /// <summary> Called when the world object is placed by a chunk. </summary>
    public abstract void on_placement();

    /// <summary> Called when the world object is placed by a chunk. </summary>
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

    /// <summary> Should return false if this world object is incompatible 
    /// with the given terrain normal. </summary>
    public virtual bool can_place(Vector3 terrain_normal)
    {
        return true;
    }

    /// <summary> Should return false if this world object is incompatible 
    /// with the given <see cref="biome.point"/>. </summary>
    public virtual bool can_place(biome.point p)
    {
        return true;
    }

    /// <summary> Load a world object from the resources/world_objects
    /// folder (saves results in a dictionary for speedy access). </summary>
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
    static Dictionary<string, world_object> _library;

    /// <summary> Loads a building_generator from the resources/buildings
    /// folder (saves results in a dictionary for speedy access). </summary>
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
    static Dictionary<string, building_generator> _building_library;
}