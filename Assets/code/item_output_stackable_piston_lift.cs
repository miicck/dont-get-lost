using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_output_stackable_piston_lift : item_output
{
    public float closely_connected_distance = 0.5f;

    protected override bool can_output_to(item_node other)
    {
        // Get all the other piston outputs for this building
        var bm = GetComponentInParent<building_material>();
        if (bm == null) return base.can_output_to(other);
        var all_outputs = bm.GetComponentsInChildren<item_output_stackable_piston_lift>();
        if (all_outputs.Length == 0) return base.can_output_to(other);

        // Can't connect to myself
        if (other.GetComponentInParent<building_material>() ==
            GetComponentInParent<building_material>())
            return false;

        // Figure out if any of the other outputs from
        // this piston lift are "closely-connected"
        bool has_close_output = false;
        foreach (var o in all_outputs)
            o.iterate_outputs((target_node) =>
            {
                Vector3 target = target_node.input_point(o.output_point);
                if ((target - o.output_point).magnitude <= closely_connected_distance)
                    has_close_output = true;
                return has_close_output;
            });
        if (!has_close_output)
            return base.can_output_to(other); // No close outputs found => normal output behaviour

        // Remove any "far-connected" outputs
        List<KeyValuePair<item_node, item_node>> to_remove = new List<KeyValuePair<item_node, item_node>>();
        foreach (var o in all_outputs)
            o.iterate_outputs((target_node) =>
            {
                Vector3 target = target_node.input_point(o.output_point);
                if ((target - o.output_point).magnitude > closely_connected_distance)
                    to_remove.Add(new KeyValuePair<item_node, item_node>(o, target_node));
                return false;
            });

        foreach (var kv in to_remove)
        {
            break_connection(kv.Key, kv.Value);
            kv.Key.refresh_display();
            kv.Value.refresh_display();
        }

        // Only allow close connections
        return (other.input_point(output_point) - output_point).magnitude <= closely_connected_distance;
    }

#if UNITY_EDITOR
    [UnityEditor.CanEditMultipleObjects]
    [UnityEditor.CustomEditor(typeof(item_output_stackable_piston_lift))]
    class editor : UnityEditor.Editor
    {

    }
#endif
}
