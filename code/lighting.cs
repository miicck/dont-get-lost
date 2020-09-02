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
        float t = time_manager.get_time();
        float b = brightness_from_time(t);

        // Work out the sun brightness from the raw brightness
        float sb = b * 0.75f + 0.25f;
        sun.color = new Color(sb, sb, sb);

        // Sun moves in sky - looks kinda fast/does wierd things to the shadows
        if (options_menu.get_bool("moving_sun"))
            sun.transform.forward = new Vector3(
                Mathf.Cos(Mathf.PI * t),
                -Mathf.Sin(Mathf.PI * t) - 0.5f,
                0
            );
        else
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Set the ambient brightness according to the desired sky color
        UnityEngine.Rendering.HighDefinition.GradientSky sky;
        if (options_menu.global_volume.profile.TryGet(out sky))
        {
            // Work out ambient brightness as a function of raw brightness

            float tb = b * 0.015f; // Top brightness (low in daytime - mostly provided by sun, zero at night)
            float mb = b * 0.15f; // Middle brightness (medium in daytime, zero at night)
            float bb = b * 0.26f + 0.04f; // Bottom brightness (bright in daytime, non-zero at night - so we can see)

            sky.top.value = new Color(tb, tb, tb);
            sky.middle.value = new Color(mb, mb, mb);
            sky.bottom.value = new Color(bb, bb, bb);
        }

        Color target_sky_color = sky_color_daytime;
        target_sky_color.r *= b;
        target_sky_color.g *= b;
        target_sky_color.b *= b;
        sky_color = Color.Lerp(sky_color, target_sky_color, Time.deltaTime * 5f);

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

    float brightness_from_time(float time)
    {
        const float DAWN_FRACTION = 0.1f;
        const float DUSK_FRACTION = 0.1f;

        // Dawn (end of night)
        if (time > 2f - DAWN_FRACTION)
            return 1f - (2f - time) / DAWN_FRACTION;

        // Night
        if (time > 1f)
            return 0f;

        // Dusk (end of day)
        if (time > 1f - DUSK_FRACTION)
            return (1f - time) / DUSK_FRACTION;

        // Day
        return 1f;
    }

    //##################//
    // STATIC INTERFACE //
    //##################//

    static lighting manager;
}
