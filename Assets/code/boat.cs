using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class boat : networked, IInspectable
{
    const int TOTAL_JOURNEY_TIME = 10;

    networked_variables.net_int away_time;
    networked_variables.net_bool outward_journey;
    networked_variables.net_string_counts_v2 contents;

    public float journey_percentage =>
        Mathf.Round(100f * (away_time.value / (float)TOTAL_JOURNEY_TIME));

    dock dock => GetComponentInParent<dock>();

    bool visible
    {
        set
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = value;
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = value;
        }
    }

    public int total_cargo
    {
        get
        {
            int ret = 0;
            foreach (var kv in contents)
                ret += kv.Value;
            return ret;
        }
    }

    public int total_cargo_value
    {
        get
        {
            int ret = 0;
            foreach (var kv in contents)
                ret += Resources.Load<item>("items/" + kv.Key).value * kv.Value;
            return ret;
        }
    }

    public void launch()
    {
        outward_journey.value = true;
    }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        contents = new networked_variables.net_string_counts_v2();
        outward_journey = new networked_variables.net_bool();
        away_time = new networked_variables.net_int();

        away_time.on_change = () =>
        {
            visible = away_time.value == 0;
            if (away_time.value >= TOTAL_JOURNEY_TIME)
            {
                outward_journey.value = false;
                int coins = total_cargo_value;
                contents.clear();
                contents["coin"] = coins;
            }
        };
    }

    float accumulated_away_time;

    private void Update()
    {
        // Stay floating level
        Vector3 fw = transform.forward;
        fw.y = 0;
        transform.rotation = Quaternion.LookRotation(fw, Vector3.up);

        if (!has_authority) return;

        Vector3 target = outward_journey.value ?
            dock.transform.position + dock.transform.forward * 5 :
            dock.transform.position;

        // Stay at sea level
        target.y = world.SEA_LEVEL;

        bool arrived = false;
        if (utils.move_towards(transform, target, Time.deltaTime))
        {
            arrived = true;
            if (outward_journey.value)
            {
                // Accumulate time away
                accumulated_away_time += Time.deltaTime;
                if (accumulated_away_time > 1)
                {
                    away_time.value += 1;
                    accumulated_away_time = 0;
                }
            }
            else
            {
                away_time.value = 0;

                if (contents["coin"] > 0)
                {
                    contents["coin"] -= 1;
                    var co = dock.coins_output;
                    co.add_item(item.create(
                        "coin", co.transform.position,
                        Quaternion.identity, logistics_version: true));
                }
            }
        }

        visible = !(outward_journey.value && arrived);
    }

    public void add_item(item i)
    {
        contents[i.name] += 1;

        Debug.Log(i.display_name);
        i.delete();
    }

    //##############//
    // IINspectable //
    //##############//

    public string inspect_info()
    {
        string ret = "Boat\n";
        ret += "Cargo (total value = " + total_cargo_value.qs() + " coins):\n";
        foreach (var kv in contents)
            ret += "    " + kv.Value.qs() + " " + kv.Key + "\n";
        return ret;
    }

    public Sprite main_sprite() { return null; }
    public Sprite secondary_sprite() { return null; }
}
