using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that automatically harvests <see cref="harvestable"/> things, 
/// with no input from <see cref="player"/>s or <see cref="settler"/>s. </summary>
public class auto_harvester : building_material, IPlayerInteractable
{
    // Determines where we check for harvestable objects
    public Transform ray_start;
    public float ray_length = 1f;

    // Determines what, and how fast, we are harvesting
    public tool.TYPE tool_type;
    public tool.QUALITY tool_quality;
    public float time_between_harvests = 1f;

    // The harvestable object, and specific
    // product of which, we are harvesting
    harvestable harvesting;
    int current_product = 0;
    float next_harvest_time = 0;

    item_output output => GetComponentInChildren<item_output>();

    void Start()
    {
        InvokeRepeating("validate_harvesting", 0, 0.5f);
    }

    void validate_harvesting()
    {
        if (this == null) return; // Destroyed

        // Figure out what we're harvesting
        harvesting = utils.raycast_for_closest<harvestable>(
            new Ray(ray_start.position, ray_start.forward),
            out RaycastHit hit, ray_length, (hit, h) =>
            {
                return h.tool.tool_type == tool_type &&
                                   h.tool.tool_quality <= tool_quality;
            });
    }

    private void Update()
    {
        if (harvesting == null) return;

        if (output.item_count == 0 && Time.time > next_harvest_time)
        {
            // Work out time of next harvest
            next_harvest_time = Time.time + time_between_harvests;

            // Cycle output products
            current_product = (current_product + 1) % harvesting.products.Length;
            var next_harvest = harvesting.products[current_product].auto_item;

            // Create output product
            output.add_item(create(
                next_harvest.name, output.transform.position,
                output.transform.rotation, logistics_version: true));
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the harvesting ray
        if (ray_start == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(ray_start.position,
            ray_start.position + ray_start.forward * ray_length);
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public override player_interaction[] player_interactions()
    {
        return base.player_interactions().prepend(new player_inspectable(transform)
        {
            text = () =>
            {
                string prod = "Nothing";
                if (harvesting != null)
                    prod = harvesting.products[current_product].auto_item.display_name;

                // Report what we are harvesting
                return display_name + "\n" +
                       "Tool type    : " + tool.type_to_name(tool_type) + "\n" +
                       "Tool quality : " + tool.quality_to_name(tool_quality) + "\n" +
                       "Harvesting   : " + prod;
            }
        });
    }
}
