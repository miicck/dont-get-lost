using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mood_effect : networked
{
    public int delta_mood;
    public int timespan;
    public string display_name;
    public string description;

    private void Start()
    {
        InvokeRepeating("check_timespan", 1f, 1f);
    }

    void check_timespan()
    {
        if (client.server_time > time_created.value + timespan)
        {
            CancelInvoke("check_timespan");
            delete();
        }
    }

    //############//
    // Networking //
    //############//

    networked_variables.net_int time_created;

    public override void on_init_network_variables()
    {
        time_created = new networked_variables.net_int();
    }

    public override void on_first_create()
    {
        time_created.value = client.server_time;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static mood_effect load(string name)
    {
        string prefab = "mood_effects/" + name;
        var ret = Resources.Load<mood_effect>(prefab);
        if (ret == null) Debug.LogError("Unkown mood effect " + prefab);
        return ret;
    }

    public static List<mood_effect> get_all(settler s)
    {
        // Get the event-based mood effects on this settler
        List<mood_effect> ret = new List<mood_effect>(s.GetComponentsInChildren<mood_effect>());

        // Compute the status-based mood effects on this settler
        int t = s.tiredness.value;
        if (t > 75) ret.Add(load("exhausted"));
        else if (t > 50) ret.Add(load("tired"));
        else if (t < 25) ret.Add(load("well_rested"));

        int ms = s.nutrition.metabolic_satisfaction;
        if (s.starving) ret.Add(load("starving"));
        else if (ms < 64) ret.Add(load("ravenous"));
        else if (ms < 128) ret.Add(load("hungry"));
        else if (ms > 190) ret.Add(load("well_fed"));

        return ret;
    }
}
