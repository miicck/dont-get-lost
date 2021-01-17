using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dock : building_material, IAddsToInspectionText
{
    public item_input dock_input;
    public item_input boat_input;
    public item_output coins_output;
    public item_output overflow_output;

    public crane crane;
    item picked_up_item;

    bool at_sea_level => Mathf.Abs(transform.position.y - world.SEA_LEVEL) < 1f;
    bool has_water_access = false;
    boat boat;

    /// <summary> Returns true if the boat is currently docked. </summary>
    bool boat_docked
    {
        get
        {
            if (boat == null) return false;
            Vector3 delta = boat.transform.position - transform.position;
            delta.y = 0;
            return delta.magnitude < 0.5f;
        }
    }

    private void Start()
    {
        if (at_sea_level)
            chunk.add_generation_listener(transform, (c) =>
            {
                var t = utils.raycast_for_closest<TerrainCollider>(
                    new Ray(transform.position, Vector3.down), out RaycastHit hit);

                if (t == null) return;
                has_water_access = hit.point.y < world.SEA_LEVEL;

                if (has_water_access && boat == null)
                {
                    add_register_listener(() =>
                    {
                        // Create my boat
                        Vector3 boat_pos = transform.position;
                        boat_pos.y = world.SEA_LEVEL;
                        client.create(boat_pos, "misc/boat", parent: this, rotation: transform.rotation);
                    });
                }
            });

        // When the boat gets input, put it into the boat
        boat_input.add_on_change_listener(() =>
        {
            foreach (var i in boat_input.relesae_all_items())
            {
                if (boat_docked)
                {
                    boat.add_item(i);
                    if (boat.total_cargo > 99)
                        boat.launch();
                }
                else
                    item_dropper.create(i, i.transform.position, null);
            }
        });

        // Crane starts picking up
        crane_state = CRANE_STATE.PICKUP;
    }

    enum CRANE_STATE
    {
        PICKUP,
        DROPOFF_BOAT,
        DROPOFF_OVERFLOW,
        WAITING_FOR_PICKUP,
    }

    CRANE_STATE crane_state
    {
        get => _crane_state;
        set
        {
            switch (value)
            {
                case CRANE_STATE.PICKUP:
                    crane.target = dock_input.transform.position;
                    crane.on_arrive = () =>
                    {
                        // Wait for item
                        crane_state = CRANE_STATE.WAITING_FOR_PICKUP;
                        return;
                    };
                    break;

                case CRANE_STATE.DROPOFF_BOAT:
                    crane.target = boat_input.transform.position;
                    crane.on_arrive = () =>
                    {
                        if (!boat_docked)
                        {
                            // No boat docked, take to overflow
                            crane_state = CRANE_STATE.DROPOFF_OVERFLOW;
                            return;
                        }

                        // Give item to boat
                        boat_input.add_item(picked_up_item);
                        picked_up_item = null;
                        crane_state = CRANE_STATE.PICKUP;
                    };
                    break;

                case CRANE_STATE.DROPOFF_OVERFLOW:
                    crane.target = overflow_output.transform.position;
                    crane.on_arrive = () =>
                    {
                        // Drop off item at overflow, get next item
                        overflow_output.add_item(picked_up_item);
                        picked_up_item = null;
                        crane_state = CRANE_STATE.PICKUP;
                    };
                    break;

                case CRANE_STATE.WAITING_FOR_PICKUP:

                    // Attempt pickup
                    attempt_pickup();
                    break;
            }
            _crane_state = value;
        }
    }
    CRANE_STATE _crane_state;

    void attempt_pickup()
    {
        if (dock_input.item_count == 0)
        {
            // Try again later
            Invoke("attempt_pickup", 0.1f);
            return;
        }

        // Pick up item + take to boat
        picked_up_item = dock_input.release_item(0);
        picked_up_item.transform.SetParent(crane.hook);
        picked_up_item.transform.localPosition = Vector3.zero;
        crane_state = boat_docked ? CRANE_STATE.DROPOFF_BOAT : CRANE_STATE.DROPOFF_OVERFLOW;
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (has_water_access)
        {
            if (boat.journey_percentage > 0.1f)
                return "Boat has completed " + boat.journey_percentage + "% of its journey.";
            return "Has water access.";
        }
        else return "No water access!";
    }

    //###########//
    // NETWORKED //
    //###########//

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        // Assign my boat
        if (child is boat)
            boat = (boat)child;
    }
}