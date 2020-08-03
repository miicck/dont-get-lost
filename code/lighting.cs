using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    void Start()
    {
        // Allow static access
        manager = this;

        // Set the initial sun position + orientation
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
    }

    void Update()
    {
        float b = brightness_from_time(time_manager.get_time());
        sun.enabled = b != 0f;
        sun.color = new Color(b,b,b);

        UnityEngine.Rendering.HighDefinition.GradientSky sky;
        if (options_menu.global_volume.profile.TryGet(out sky))
        {
            sky.multiplier.value = b * 0.2f + 0.1f;
            options_menu.global_volume.profile.isDirty = true;
        }

        Color target_sky_color = sky_color_daytime;
        target_sky_color.r *= b;
        target_sky_color.g *= b;
        target_sky_color.b *= b;
        sky_color = Color.Lerp(sky_color, target_sky_color, Time.deltaTime*5f);
    }

    float brightness_from_time(float time)
    {
        const float DAWN_FRACTION = 0.1f;
        const float DUSK_FRACTION = 0.1f;

        // Dawn (end of night)
        if (time > 2f - DAWN_FRACTION)
            return 1f - (2f - time)/DAWN_FRACTION;

        // Night
        if (time > 1f)
            return 0f;

        // Dusk (end of day)
        if (time > 1f - DUSK_FRACTION)
            return (1f - time)/DUSK_FRACTION;

        // Day
        return 1f;
    }

    //##################//
    // STATIC INTERFACE //
    //##################//

    static lighting manager;
}
