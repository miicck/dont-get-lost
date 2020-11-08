using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_resource_gatherer : settler_interactable, IAddsToInspectionText
{
    public item_output output;
    public Transform ray_start;
    public float ray_length;

    public float time_between_harvests = 1f;
    public int max_harvests = 5;

    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    harvestable harvesting;
    float time_harvesting;
    int harvested_count = 0;

    private void OnDrawGizmos()
    {
        // Draw the harvesting ray
        if (ray_start == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(ray_start.position,
            ray_start.position + ray_start.forward * ray_length);
    }

    protected override void Start()
    {
        base.Start();
        Invoke("update_harvesting", 1);
    }

    public string added_inspection_text()
    {
        if (harvesting == null)
            return "    Noting in harvest range.";

        return "    Harvesting " + product.product_plurals_list(harvesting.products);
    }

    void update_harvesting()
    {
        // Figure out what we're harvesting
        harvesting = utils.raycast_for_closest<harvestable>(
            new Ray(ray_start.position, ray_start.forward),
            out RaycastHit hit, ray_length, (h) =>
            {
                return h.tool.tool_type == tool_type &&
                                   h.tool.tool_quality <= tool_quality;
            });

        // Try again later
        if (harvesting == null)
            Invoke("update_harvesting", 1);
    }

    //##############//
    // INTERACTABLE //
    //##############//

    public override void on_assign(settler s)
    {
        // Reset stuff 
        time_harvesting = 0f;
        harvested_count = 0;
    }

    public override void on_interact(settler s)
    {
        // Record how long has been spent harvesting
        time_harvesting += Time.deltaTime;

        if (time_harvesting > harvested_count)
        {
            harvested_count += 1;

            // Create the products
            if (harvesting != null)
                foreach (var p in harvesting.products)
                    p.create_in_node(output);
        }
    }

    public override bool is_complete(settler s)
    {
        // We're done if we've harvested enough times
        return harvested_count >= max_harvests;
    }

    public override void on_unassign(settler s) { }
}
