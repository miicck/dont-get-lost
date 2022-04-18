using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weather_manager : networked
{
    networked_variables.net_int next_weather_time;
    networked_variables.net_string next_weather;

    public override float network_radius() => Mathf.Infinity; // Always loaded

    public override void on_create()
    {
        // Ensure only one manager exists
        utils.delete_all_but_oldest(FindObjectsOfType<weather_manager>());
    }

    public override void on_init_network_variables()
    {
        next_weather_time = new networked_variables.net_int();
        next_weather = new networked_variables.net_string(default_value: "sun");

        // It is assumed that the weather name will change after the time,
        // so that when the name changes, both will be updated.
        next_weather.on_change = () =>
        {
            int time_from_now = next_weather_time.value - client.server_time;
            if (time_from_now < 0) time_from_now = 0;
            weather.queue_weather_event(Resources.Load<weather>("weathers/" + next_weather.value), time_from_now);
        };
    }

    public void trigger_weather(string name, int server_time)
    {
        var weather = Resources.Load<weather>("weathers/" + name);
        if (weather == null)
        {
            Debug.LogError("Unkown weather: " + name);
            return;
        }

        if (!has_authority)
            return; // Only trigger weather events on authority client

        next_weather_time.value = server_time;
        next_weather.value = name; // This must be set second
    }

    weather pick_next_weather()
    {
        var weathers = Resources.LoadAll<weather>("weathers");
        float total_prob = 0;
        foreach (var w in weathers) total_prob += w.probability;

        float rand = Random.Range(0, total_prob);
        total_prob = 0;
        foreach (var w in weathers)
        {
            total_prob += w.probability;
            if (rand < total_prob)
                return w;
        }

        return weathers[weathers.Length - 1];
    }

    private void Update()
    {
        const int TRANSITION_TIME = 15;
        const int CYCLE_TIME = 60 * 2;

        if (!has_authority)
            return; // Only authority client runs the weather

        if (client.server_time <= next_weather_time.value)
            return; // Next weather already queued

        // Randomize weather every CYCLE_TIME seconds
        if (client.server_time % CYCLE_TIME == 0)
            trigger_weather(pick_next_weather().name, client.server_time + TRANSITION_TIME);
    }
}
