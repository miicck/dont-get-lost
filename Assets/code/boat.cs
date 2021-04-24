using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class boat : networked, IPlayerInteractable
{
    const int TOTAL_JOURNEY_TIME = 10;

    networked_variables.net_int away_time;
    networked_variables.net_int journey_stage;
    networked_variables.net_string_counts contents;

    enum JOURNEY_STAGES
    {
        DOCKED = 0,
        OUTWARD = 1,
        AWAY = 2,
        RETURN = 3
    }

    public float journey_percentage =>
        Mathf.Round(100f * (away_time.value / (float)TOTAL_JOURNEY_TIME));

    dock dock => GetComponentInParent<dock>();

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
        journey_stage.value = (int)JOURNEY_STAGES.OUTWARD;
    }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        contents = new networked_variables.net_string_counts();
        away_time = new networked_variables.net_int();
        journey_stage = new networked_variables.net_int();

        journey_stage.on_change = () =>
        {
            var js = (JOURNEY_STAGES)journey_stage.value;
            bool visible = true;

            switch (js)
            {
                case JOURNEY_STAGES.AWAY:
                    visible = false; // Invisible when away
                    break;

                default:
                    away_time.value = 0; // Reset away time if not away
                    visible = true; // Visible if not away
                    break;
            }

            // Set visibility appropriate to journey stage
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = visible;
        };
    }

    float accumulated_away_time;

    private void Update()
    {
        var js = (JOURNEY_STAGES)journey_stage.value;

        switch (js)
        {
            case JOURNEY_STAGES.DOCKED:

                // Drop off coins
                if (contents["coin"] > 0)
                {
                    contents["coin"] -= 1;
                    var co = dock.coins_output;
                    production_tracker.register_product("coin");

                    co.add_item(item.create(
                        "coin", co.transform.position,
                        Quaternion.identity, logistics_version: true));
                }

                break;

            case JOURNEY_STAGES.OUTWARD:
            case JOURNEY_STAGES.RETURN:

                // Only authority client sails
                if (!has_authority) break;

                // Sail towards either the away point, or back to the dock
                Vector3 target = (js == JOURNEY_STAGES.OUTWARD) ?
                    dock.transform.position + dock.transform.forward * 5 :
                    dock.transform.position;

                // Stay at sea level
                target.y = world.SEA_LEVEL;

                if (utils.move_towards(transform, target, Time.deltaTime))
                {
                    if (js == JOURNEY_STAGES.OUTWARD)
                        journey_stage.value = (int)JOURNEY_STAGES.AWAY;
                    else
                        journey_stage.value = (int)JOURNEY_STAGES.DOCKED;
                }

                break;

            case JOURNEY_STAGES.AWAY:

                // Only authority client accumulates away time
                if (!has_authority) break;

                // Accumulate time away
                accumulated_away_time += Time.deltaTime;
                if (accumulated_away_time > 1)
                {
                    away_time.value += 1;
                    accumulated_away_time = 0;

                    if (away_time.value >= TOTAL_JOURNEY_TIME)
                    {
                        // Trade contents for coins
                        int coins = total_cargo_value;
                        contents.clear();
                        contents["coin"] = coins;

                        // Start making our way home
                        journey_stage.value = (int)JOURNEY_STAGES.RETURN;
                    }
                }

                break;
        }

        // Stay floating level
        Vector3 fw = transform.forward; fw.y = 0;
        transform.rotation = Quaternion.LookRotation(fw, Vector3.up);

        // Update networked position on authority client
        if (has_authority)
            networked_position = transform.position;
    }

    public void add_item(item i)
    {
        contents[i.name] += 1;
        i.delete();
    }

    //##############//
    // IINspectable //
    //##############//

    public player_interaction[] player_interactions()
    {
        return new player_interaction[] {new player_inspectable(transform)
        {
            text = () =>
            {
                string ret = "Boat\n";
                ret += "Cargo (total value = " + total_cargo_value.qs() + " coins):\n";
                foreach (var kv in contents)
                    ret += "    " + kv.Value.qs() + " " + kv.Key + "\n";
                return ret;
            }
        }};
    }
}
