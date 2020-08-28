using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Responsible for keeping track of 
/// time, the day/night cycle etc. </summary>
public class time_manager : networked
{
    public const float DAY_LENGTH = 60f * 5;
    public const float NIGHT_LENGTH = 60f * 2;

    /// <summary> The time of day in [0,2). Values in [0,1] correspond to
    /// daytime, values in [1,2) correspond to nighttime. </summary>
    networked_variables.net_float time_of_day;

    /// <summary> The number of days passed since the 
    /// start of the server. </summary> 
    networked_variables.net_int day_number;

    public override void on_init_network_variables()
    {
        time_of_day = new networked_variables.net_float(resolution: 0.01f);
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

    public static float get_time()
    {
        if (manager == null) return 0f;
        return manager.time_of_day.value;
    }

    public static void set_time(float value)
    {
        if (manager == null) return;
        manager.time_of_day.value = value;
    }

    public static int get_day()
    {
        if (manager == null) return 0;
        return manager.day_number.value;
    }

    public static string info()
    {
        return "    Day " + get_day() + " time " + get_time();
    }
}
