using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dock : building_material, IAddsToInspectionText
{
    public item_input boat_input;

    bool at_sea_level => Mathf.Abs(transform.position.y - world.SEA_LEVEL) < 1f;
    bool has_water_access = false;
    boat boat;

    bool boat_docked
    {
        get
        {
            if (boat == null) return false;
            return (boat.transform.position - transform.position).magnitude < 0.5f;
        }
    }

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

        boat_input.add_on_change_listener(on_input_change);
    }

    void on_input_change()
    {
        foreach (var i in boat_input.relesae_all_items())
        {
            if (boat_docked)
            {
                boat.add_item(i);
                if (boat.total_cargo > 1)
                    boat.launch();
            }
            else
                item_dropper.create(i, i.transform.position, null);
        }
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