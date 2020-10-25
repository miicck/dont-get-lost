using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_resource_gatherer : settler_interactable
{
    public item_output output;
    public Transform ray_start;
    public float ray_length;

    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    harvestable harvesting;

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
        // Figure out what we're harvesting
        harvesting = utils.raycast_for_closest<harvestable>(
            new Ray(ray_start.position, ray_start.forward),
            out RaycastHit hit, ray_length, (h) =>
            {
                return h.tool.tool_type == tool_type &&
                                   h.tool.tool_quality <= tool_quality;
            });

        base.Start();
    }

    public override bool interact(settler s, float time_elapsed)
    {
        // It takes 1 second to harvest
        if (time_elapsed < 1f)
            return false;

        foreach (var p in harvesting.products)
            p.create_in_node(output);

        return true;
    }
}
