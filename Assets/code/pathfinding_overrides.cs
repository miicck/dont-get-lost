using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pathfinding_overrides : MonoBehaviour, pathfinding_settings
{
    public float max_terrain_angle = 45f;
    public float ground_clearance_fraction = 0.5f;
    public bool ignore_terrain = false;

    public float max_ground_angle() => max_terrain_angle;
    public float ground_clearance() => ground_clearance_fraction;
    public bool blocked_by_terrain() => !ignore_terrain;
}
