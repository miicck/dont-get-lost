using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_harvester : item_logistic_building, IInspectable
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

    item_link_point output => item_outputs[0];

    protected override void Start()
    {
        base.Start();
        InvokeRepeating("validate_harvesting", 0, 0.5f);
    }

    void validate_harvesting()
    {
        if (this == null) return; // Destroyed

        // Figure out what we're harvesting
        harvesting = utils.raycast_for_closest<harvestable>(
            new Ray(ray_start.position, ray_start.forward),
            out RaycastHit hit, ray_length, (h) =>
            {
                return h.tool.tool_type == tool_type &&
                                   h.tool.tool_quality <= tool_quality;
            });
    }

    private void Update()
    {
        if (harvesting == null) return;

        if (output.item == null & Time.time > next_harvest_time)
        {
            // Work out time of next harvest
            next_harvest_time = Time.time + time_between_harvests;

            // Cycle output products
            current_product = (current_product + 1) % harvesting.products.Length;
            var next_harvest = harvesting.products[current_product].auto_item;

            // Create output product
            output.item = create(next_harvest.name,
                output.position, output.transform.rotation);
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

    public override string inspect_info()
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
}
