using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Ensures everything to do with water
/// reflections stays up to date. </summary>
public class water_reflections : MonoBehaviour
{
    UnityEngine.Rendering.HighDefinition.PlanarReflectionProbe probe;
    static HashSet<Renderer> waters = new HashSet<Renderer>();
    static HashSet<Renderer> water_undersides = new HashSet<Renderer>();

    public static void register_water(Renderer water)
    {
        waters.Add(water);
        update_water(water);
    }

    public static void register_water_underside(Renderer water_underside)
    {
        water_undersides.Add(water_underside);
    }

    void Start()
    {
        // Create the water reflection probe
        probe = Resources.Load
            <UnityEngine.Rendering.HighDefinition.PlanarReflectionProbe>
            ("misc/water_reflection_probe").inst();
        probe.transform.position = transform.position;
        probe.transform.rotation = transform.rotation;
        probe.transform.SetParent(transform);
        probe.influenceVolume.shape = UnityEngine.Rendering.HighDefinition.InfluenceShape.Box;
    }

    public Color color = water_colors.cyan;

    void Update()
    {
        // Get rid of deleted water
        water_undersides.RemoveWhere((u) => u == null);
        waters.RemoveWhere((w) => w == null);

        // Set reflection size to render range
        probe.influenceVolume.boxSize = new Vector3(
            game.render_range * 2f, 0.01f, 
            game.render_range * 2f);

        // Enable water underside only when the player is underwater
        foreach (var wu in water_undersides)
            wu.enabled = player.current.camera.transform.position.y < world.SEA_LEVEL;

        // Set the water alpha color
        color.a = probe.enabled ? 0.84f : 0.31f;
        foreach (var w in waters)
            utils.set_color(w.material, color);

        // Set clear color to sky color (doesn't seem to work?)
        probe.settingsRaw.cameraSettings.bufferClearing.backgroundColorHDR = lighting.sky_color;

        // Ensure probe stays at water level
        Vector3 pos = transform.position;
        pos.y = world.SEA_LEVEL;
        transform.position = pos;

        // Ensure probe enabled state is correct
        if (probe.enabled != should_reflect)
        {
            probe.enabled = should_reflect;
            foreach (var w in waters)
                update_water(w);
        }
    }

    static void update_water(Renderer water)
    {
        water.material = Resources.Load<Material>(
                    should_reflect ?
                    "materials/standard_shader/water_reflective" :
                    "materials/standard_shader/water_normal"
                    );
    }

    public static float water_range = 256;
    public static bool reflections_enabled = true;
    static bool should_reflect => reflections_enabled && !player.current.map_open;
}
