using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Responsible for keeping track of 
/// time, the day/night cycle etc. </summary>
public class time_manager : networked
{
    public const float DAY_LENGTH = 60f * 5;
    public const float NIGHT_LENGTH = 60f * 2;
    public const float DAWN_FRAC = 0.1f;
    public const float DUSK_FRAC = 0.1f;

    /// <summary> The time of day in [0,2). Values in [0,1] correspond to
    /// daytime, values in [1,2) correspond to nighttime. </summary>
    networked_variables.net_float time_of_day;

    /// <summary> The number of days passed since the 
    /// start of the server. </summary> 
    networked_variables.net_int day_number;

    public override void on_init_network_variables()
    {
        time_of_day = new networked_variables.net_float(resolution: 0.01f, lerp_speed: 0.1f);
        day_number = new networked_variables.net_int();
        manager = this;
    }

    void Update()
    {
        // Only the authoriy client controls time
        if (!has_authority) return;

        // Increment time
        if (time_of_day.value < 1f)
            time_of_day.value += Time.deltaTime / DAY_LENGTH;
        else if (time_of_day.value < 2f)
            time_of_day.value += Time.deltaTime / NIGHT_LENGTH;
        else
        {
            // Increment day count, reset time
            time_of_day.value = 0;
            day_number.value += 1;
        }
    }

    public override float network_radius()
    {
        // The time manager is always loaded
        return float.PositiveInfinity;
    }

    //##################//
    // STATIC INTERFACE //
    //##################//

    static time_manager manager;

    public static float time
    {
        get => manager == null ? 0f : manager.time_of_day.lerped_value;
        set
        {
            if (manager == null) return;
            manager.time_of_day.value = value;
        }
    }

    public static int day => manager == null ? 0 : manager.day_number.value;

    public static float day_amount
    {
        get
        {
            float t = time;

            // Daytime
            if (t < 1 - DUSK_FRAC)
                return 1f;

            // First half of dusk
            if (t < 1 - DUSK_FRAC / 2f)
                return (1 - DUSK_FRAC / 2f - t) / (DUSK_FRAC / 2f);

            // Second half of dusk/Night/First half of dawn
            if (t < 2 - DAWN_FRAC / 2f)
                return 0f;

            // Last half of dawn
            return (t - (2 - DAWN_FRAC / 2f)) / (DAWN_FRAC / 2f);
        }
    }

    public static float night_amount
    {
        get
        {
            float t = time;

            // Daytime/First half of dusk
            if (t < 1 - DUSK_FRAC / 2f)
                return 0f;

            // Second half of dusk
            if (t < 1f)
                return (t - (1 - DUSK_FRAC / 2f)) / (DUSK_FRAC / 2f);

            // Night
            if (t < 2 - DAWN_FRAC)
                return 1f;

            // First half of dawn
            if (t < 2 - DAWN_FRAC / 2f)
                return (2f - DAWN_FRAC / 2f - t) / (DAWN_FRAC / 2f);

            // Second half of dawn
            return 0f;
        }
    }

    public static float dawn_amount
    {
        get
        {
            float t = time;

            // Not dawn
            if (t < 2f - DAWN_FRAC) return 0;

            // First half of dawn
            if (t < 2f - DAWN_FRAC / 2f)
                return (t - (2 - DAWN_FRAC)) / (DAWN_FRAC / 2f);

            // Second half of dawn
            return (2 - t) / (DAWN_FRAC / 2f);
        }
    }

    public static float dusk_amount
    {
        get
        {
            float t = time;

            // Not dusk
            if (t < 1 - DUSK_FRAC) return 0;
            if (t > 1) return 0;

            // First half of dusk
            if (t < 1 - DUSK_FRAC / 2f)
                return 1 - ((1 - DUSK_FRAC / 2f) - t) / (DUSK_FRAC / 2f);

            // Second half of dusk
            return (1 - t) / (DUSK_FRAC / 2f);
        }
    }

    public static float time_to_brightness
    {
        get
        {
            // Dawn (end of night)
            if (time > 2f - DAWN_FRAC)
                return 1f - (2f - time) / DAWN_FRAC;

            // Night
            if (time > 1f)
                return 0f;

            // Dusk (end of day)
            if (time > 1f - DUSK_FRAC)
                return (1f - time) / DUSK_FRAC;

            // Day
            return 1f;
        }
    }

    public static string info()
    {
        return "    Day " + day + " time " + time + "\n" +
               "    Dawn/Day/Dusk/Night : " +
               System.Math.Round(dawn_amount, 2) + "/" +
               System.Math.Round(day_amount, 2) + "/" +
               System.Math.Round(dusk_amount, 2) + "/" +
               System.Math.Round(night_amount, 2);
    }
}
