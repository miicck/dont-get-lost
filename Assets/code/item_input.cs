using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An item input node to an object, such as an autocrafter. </summary>
public class item_input : item_node
{
    protected override bool can_input_from(item_node other)
    {
        if (this == null || other == null)
            return false;

        Vector3 delta = input_point(other.output_point) - other.output_point;
        if (delta.y > UPHILL_LINK_ALLOW) return false; // Can't accept input from below

        if (delta.magnitude < LINK_DISTANCE_TOLERANCE)
            return true; // Close enough for direct node

        // Check if from/to are in line for a drop
        Vector3 dxz = new Vector3(delta.x, 0, delta.z);
        if (dxz.magnitude > LINK_DISTANCE_TOLERANCE)
            return false; // Don't line up properly

        // Check if the drop is possible
        foreach (var h in Physics.RaycastAll(other.output_point, delta, delta.magnitude))
        {
            if (h.transform.GetComponentInParent<item>() != null)
                continue; // Ignore collisions with items
            return false; // Something in the way of the drop
        }

        return true;
    }

    protected override bool on_add_item(item i)
    {
        // Only one item allowed in input
        if (item_count > 0)
        {
            Destroy(i.gameObject);
            return false;
        }
        return true;
    }

    protected override bool can_output_to(item_node other)
    {
        // Inputs can't output to anything
        return false;
    }
}
