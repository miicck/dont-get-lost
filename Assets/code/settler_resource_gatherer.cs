using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_resource_gatherer : settler_interactable, IAddsToInspectionText
{
    public item_output output;
    public Transform ray_start;
    public float ray_length;

    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    harvestable harvesting;
    float time_harvesting;

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
    }

    public override void on_interact(settler s)
    {
        // Record how long has been spent harvesting
        time_harvesting += Time.deltaTime;
    }

    public override bool is_complete(settler s)
    {
        // It takes 1 second to finish harvesting
        return time_harvesting > 1f;
    }

    public override void on_unassign(settler s)
    {
        // Only create item on authority client
        if (!s.has_authority) return;

        // Create the products
        foreach (var p in harvesting.products)
            p.create_in_node(output);
    }
}
