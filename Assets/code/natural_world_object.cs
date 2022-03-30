using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class natural_world_object : world_object
{
    // Variables specifying how this world object is placed
    public float align_with_terrain_normal = 0f;
    public Y_ROTATION_MODE y_rotation_mode;
    public float min_altitude = world.SEA_LEVEL;
    public float max_altitude = world.MAX_ALTITUDE;
    public float max_terrain_angle = 90f;
    public float min_terrain_angle = 0f;
    public float random_move_amplitude = 0f;
    public bool inherit_terrain_color = false;
    public float fixed_altitude = -1;
    public Renderer randomize_color_on;
    public Vector3 min_scales;
    public Vector3 max_scales;
    public bool seperate_scale_factor = false;

    // The way that y rotation is chosen for this object
    public enum Y_ROTATION_MODE
    {
        NONE,
        RANDOM,
        ALLIGN_Z_DOWNHILL
    }

    public override bool can_place(biome.point p)
    {
        if (p.altitude > max_altitude) return false;
        if (p.altitude < min_altitude) return false;
        return true;
    }

    public override bool can_place(Vector3 terrain_normal)
    {
        float angle = Vector3.Angle(terrain_normal, Vector3.up);
        if (angle > max_terrain_angle) return false;
        if (angle < min_terrain_angle) return false;
        return true;
    }

    void carry_out_terrain_alignment(Vector3 terrain_normal, biome.point point)
    {
        if (align_with_terrain_normal > 0)
        {
            transform.up = align_with_terrain_normal * terrain_normal +
                      (1 - align_with_terrain_normal) * Vector3.up;
        }

        if (y_rotation_mode == Y_ROTATION_MODE.ALLIGN_Z_DOWNHILL)
        {
            Vector3 downhill = terrain_normal;
            downhill.y = 0;
            downhill -= align_with_terrain_normal * Vector3.Project(downhill, terrain_normal);
            transform.forward = downhill;
        }

        if (inherit_terrain_color)
            foreach (var m in GetComponentsInChildren<MeshRenderer>())
                m.material.color = point.terrain_color;
    }

    public override void on_placement()
    {
        if (randomize_color_on != null)
        {
            Color rand_col = new Color(
                chunk.random.range(0, 1f),
                chunk.random.range(0, 1f),
                chunk.random.range(0, 1f)
                );
            randomize_color_on.material.SetColor("_BaseColor", rand_col);
        }

        // Allign to terrain if required
        carry_out_terrain_alignment(terrain_normal, point);

        // Generate the scale
        if (seperate_scale_factor)
        {
            transform.localScale = new Vector3(
                chunk.random.range(min_scales.x, max_scales.x),
                chunk.random.range(min_scales.y, max_scales.y),
                chunk.random.range(min_scales.z, max_scales.z)
            );
        }
        else
        {
            float sf = chunk.random.range(0, 1f);
            transform.localScale = new Vector3(
                min_scales.x * (1 - sf) + max_scales.x * sf,
                min_scales.y * (1 - sf) + max_scales.y * sf,
                min_scales.z * (1 - sf) + max_scales.z * sf
            );
        }

        // Generate random y rotation
        if (y_rotation_mode == Y_ROTATION_MODE.RANDOM)
            transform.Rotate(0, chunk.random.range(0, 360), 0);

        if (random_move_amplitude > 0)
        {
            Vector3 delta = new Vector3(
                chunk.random.range(-random_move_amplitude, random_move_amplitude), 0,
                chunk.random.range(-random_move_amplitude, random_move_amplitude)
            );
            transform.position += delta;
        }

        if (fixed_altitude >= 0)
        {
            Vector3 pos = transform.position;
            pos.y = fixed_altitude;
            transform.position = pos;
        }

        // Stop z-fighting
        transform.position += Random.insideUnitSphere * 0.001f;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(natural_world_object), true)]
    [UnityEditor.CanEditMultipleObjects]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var wo = (natural_world_object)target;
        }
    }
#endif

}
