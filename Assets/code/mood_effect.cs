using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMoodEffect
{
    public int delta_mood { get; }
    public string display_name { get; }
    public string description { get; }
}

class simple_mood_effect : IMoodEffect
{
    public int delta_mood { get; set; }
    public string display_name { get; set; }
    public string description { get; set; }
}

public class mood_effect : networked, IMoodEffect
{
    public int timespan;

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

    //#############//
    // IMoodEffect //
    //#############//

    [SerializeField]
    int _delta_mood;
    public int delta_mood => _delta_mood;

    [SerializeField]
    string _display_name;
    public string display_name => _display_name;

    [SerializeField]
    string _description;
    public string description => _description;

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

    static void add_tiredness_mood_effects(List<IMoodEffect> add_to, settler s)
    {
        // Compute the status-based mood effects on this settler
        int t = s.tiredness.value;

        if (t > 75)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = -10,
                display_name = "Exhausted",
                description = "I am very tired"
            });
            return;
        }

        if (t > 50)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = -3,
                display_name = "Tired",
                description = "I am a little tired"
            });
            return;
        }

        if (t < 25)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = 10,
                display_name = "Well rested",
                description = "I am feeling full of energy"
            });
            return;
        }

        if (t < 50)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = 5,
                display_name = "Rested",
                description = "I am feeling rested"
            });
            return;
        }
    }

    static void add_nutrition_mood_effects(List<IMoodEffect> add_to, settler s)
    {
        // Overall satisfaction mood effects
        int ms = s.nutrition.metabolic_satisfaction;

        if (s.starving)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = -20,
                display_name = "Starving",
                description = "I am starving to death!"
            });
        }
        else if (ms < 64)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = -10,
                display_name = "Ravenous",
                description = "I am very hungry"
            });
        }
        else if (ms < 128)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = -5,
                display_name = "Hungry",
                description = "I am feeling peckish"
            });
        }
        else if (ms > 190)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = 10,
                display_name = "Well fed"
            }); ;
        }
        else if (ms > 128)
        {
            add_to.Add(new simple_mood_effect
            {
                delta_mood = 5,
                display_name = "Not hungry",
                description = "I'm not feeling hungry"
            });
        }

        // Specific food group mood effects
        foreach (var g in food.all_groups)
        {
            if (s.nutrition[g] < 64)
            {
                add_to.Add(new simple_mood_effect
                {
                    delta_mood = -1,
                    display_name = "Malnourished (" + food.group_name(g) + ")",
                    description = "I am deficient of " + food.group_name(g)
                });
            }
            else if (s.nutrition[g] > 190)
            {
                add_to.Add(new simple_mood_effect
                {
                    delta_mood = 2,
                    display_name = "Nourished (" + food.group_name(g) + ")",
                    description = "I am not lacking in " + food.group_name(g)
                });

            }
        }
    }

    public static List<IMoodEffect> get_all(settler s)
    {
        // Get the event-based mood effects on this settler
        List<IMoodEffect> ret = new List<IMoodEffect>(s.GetComponentsInChildren<mood_effect>());
        add_tiredness_mood_effects(ret, s);
        add_nutrition_mood_effects(ret, s);
        return ret;
    }
}
