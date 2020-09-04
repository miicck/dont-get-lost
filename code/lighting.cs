using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class fog_distances
{
    public const float EXTEREMELY_CLOSE = 1f;
    public const float VERY_CLOSE = 3f;
    public const float CLOSE = 8f;
    public const float MEDIUM = 20f;
    public const float FAR = 50f;
    public const float VERY_FAR = 100f;
    public const float OFF = 500f;
}

public class lighting : MonoBehaviour
{
    public Light sun;
    public Color day_sun_color;
    public Color dawn_sun_color;
    public Color dusk_sun_color;
    public Color night_sun_color;

    public static Color sky_color
    {
        get
        {
            if (player.current == null)
                return Color.black;
            return player.current.sky_color;
        }

        set
        {
            if (player.current == null)
                return;
            player.current.sky_color = value;
        }
    }

    public static Color sky_color_daytime;
    public static float fog_distance;

    void Start()
    {
        // Allow static access
        manager = this;

        // Set the initial sun position + orientation
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
    }

    static float saved_ambient_occlusion_intensity = -1;

    void Update()
    {
        // Work out the sun color from the time of day
        float day = time_manager.day_amount;
        float da = time_manager.dawn_amount;
        float ds = time_manager.dusk_amount;
        float nt = time_manager.night_amount;

        sun.color = new Color(
            day_sun_color.r * day + dawn_sun_color.r * da + dusk_sun_color.r * ds + night_sun_color.r * nt,
            day_sun_color.g * day + dawn_sun_color.g * da + dusk_sun_color.g * ds + night_sun_color.g * nt,
            day_sun_color.b * day + dawn_sun_color.b * da + dusk_sun_color.b * ds + night_sun_color.b * nt
        );

        // Sun moves in sky - looks kinda fast/does wierd things to the shadows
        if (options_menu.get_bool("moving_sun"))
            sun.transform.forward = new Vector3(
                Mathf.Cos(Mathf.PI * time_manager.time),
                -Mathf.Sin(Mathf.PI * time_manager.time) - 1.1f, // Always slightly downward
                0
            );
        else
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Overall ambient brightness
        float b = time_manager.time_to_brightness;

        // Set the ambient brightness according to the desired sky color
        UnityEngine.Rendering.HighDefinition.GradientSky sky;
        if (options_menu.global_volume.profile.TryGet(out sky))
        {
            // Work out ambient brightness as a function of raw brightness            
            float tb = b * 0.015f + 0.04f; // Top brightness (low in daytime - mostly provided by sun)
            float mb = b * 0.15f; // Middle brightness (medium in daytime, zero at night - otherwise water looks weird)
            float bb = b * 0.26f + 0.04f; // Bottom brightness (bright in daytime)

            sky.top.value = new Color(tb, tb, tb);
            sky.middle.value = new Color(mb, mb, mb);
            sky.bottom.value = new Color(bb, bb, bb);
        }

        Color target_sky_color = sky_color_daytime;
        target_sky_color.r *= b;
        target_sky_color.g *= b;
        target_sky_color.b *= b;
        sky_color = Color.Lerp(sky_color, target_sky_color, Time.deltaTime * 5f);

        // Apply time-based color adjustments
        UnityEngine.Rendering.HighDefinition.ColorAdjustments color;
        if (options_menu.global_volume.profile.TryGet(out color))
        {
            // Reduce saturation at night
            float max_saturation = options_menu.get_float("saturation");
            max_saturation = (max_saturation + 100f) / 200f;
            float sat = max_saturation * (0.5f + 0.5f * b);
            color.saturation.value = sat * 200f - 100f;
        }

        // Enable/disable fog
        if (options_menu.get_bool("fog"))
        {
            UnityEngine.Rendering.HighDefinition.Fog fog;
            if (options_menu.global_volume.profile.TryGet(out fog))
            {
                // Disable fog in map view
                if (player.current != null)
                    fog.enabled.value = !player.current.map_open;

                // Keep fog color/distance up to date
                fog.color.value = sky_color;
                fog.albedo.value = sky_color;
                fog.meanFreePath.value = Mathf.Lerp(
                    fog.meanFreePath.value, fog_distance, Time.deltaTime * 5f);
            }
        }

        // Keep ambient occlusion up-to-date
        UnityEngine.Rendering.HighDefinition.AmbientOcclusion ao;
        if (options_menu.global_volume.profile.TryGet(out ao))
        {
            if (player.current != null)
            {
                // Turn off ambient occlusion in map view
                if (saved_ambient_occlusion_intensity < 0)
                    saved_ambient_occlusion_intensity = ao.intensity.value;
                ao.intensity.value = player.current.map_open ?
                    0f : saved_ambient_occlusion_intensity;
            }
        }

        // Let the volume system know that it needs updating
        options_menu.global_volume.profile.isDirty = true;
    }

    //##################//
    // STATIC INTERFACE //
    //##################//

    static lighting manager;
}
