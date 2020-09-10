using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Splits a multiple input item flows
/// into multiple output item flows. </summary>
public class item_splitter : MonoBehaviour, INonBlueprintable
{
    public float selector_speed = 1f;

    List<item_link_point> inputs = new List<item_link_point>();
    List<item_link_point> outputs = new List<item_link_point>();

    int current_input = 0;
    int current_output = 0;

    public Transform input_selector;
    public Transform input_selector_end;

    public Transform output_selector;
    public Transform output_selector_end;

    item input_selector_item;
    item output_selector_item;

    public AXIS input_selector_axis = AXIS.Z_AXIS;
    public AXIS output_selector_axis = AXIS.Z_AXIS;
    public AXIS input_rotation_axis = AXIS.Y_AXIS;
    public AXIS output_rotation_axis = AXIS.Y_AXIS;

    private void Start()
    {
        // Record the inputs and outputs
        foreach (var lp in GetComponentsInChildren<item_link_point>())
        {
            switch (lp.type)
            {
                case item_link_point.TYPE.INPUT:
                    inputs.Add(lp);
                    break;

                case item_link_point.TYPE.OUTPUT:
                    outputs.Add(lp);
                    break;
            }
        }

        if (inputs.Count == 0)
            throw new System.Exception("No inputs to item splitter!");

        if (outputs.Count == 0)
            throw new System.Exception("No outputs to item splitter!");
    }

    bool move_selector_towards(Transform selector, item_link_point target,
        AXIS selector_axis, AXIS rotation_axis)
    {
        // Get the current selector axis direction, and the target
        // selector axis direction in the rotation plane
        Vector3 ax = selector.axis(selector_axis);
        Vector3 target_ax = (target.position - selector.position).normalized;

        Vector3 rot_axis = transform.axis(rotation_axis);
        ax -= Vector3.Project(ax, rot_axis);
        target_ax -= Vector3.Project(target_ax, rot_axis);

        float angle = Vector3.Angle(ax, target_ax);

        // Check if we're close enough
        if (angle < 5f) return true;

        // Rotate towards the target
        float sign = Mathf.Sign(
            Vector3.Dot(Vector3.Cross(ax, target_ax), rot_axis));

        float rot_amt = Mathf.Min(angle, Time.deltaTime * 45 * selector_speed);
        selector.Rotate(rot_axis, sign * rot_amt, Space.World);
        return false;
    }

    bool move_towards(Transform t, Vector3 v, float speed)
    {
        // Move the transfor t towards v at the given speed
        // returning true if arrived
        Vector3 delta = v - t.position;
        float max_move = Time.deltaTime * speed;
        bool arrived = false;
        if (delta.magnitude > max_move)
            delta = delta.normalized * max_move;
        else arrived = true;
        t.position += delta;
        return arrived;
    }

    private void Update()
    {
        // The input and output selectors cycle to the next
        // input/output when they have picked up an item

        var input = inputs[current_input];
        var output = outputs[current_output];

        // Move input selector towards current_input
        if (move_selector_towards(input_selector, input,
            input_selector_axis, input_rotation_axis))
            if (input_selector_item == null && input.item != null)
            {
                // Pickup item
                input_selector_item = input.release_item();
                input_selector_item.transform.SetParent(input_selector);
                input_selector_item.transform.position = input_selector_end.position;

                // Cycle to next input
                current_input = (current_input + 1) % inputs.Count;
            }

        // Move output selector towards current_output
        bool output_alligned = move_selector_towards(output_selector, output,
            output_selector_axis, output_rotation_axis);

        // Move output selector item along the output selector
        if (output_selector_item != null)
            if (move_towards(output_selector_item.transform,
                output_selector_end.position, 1f) && 
                output_alligned && 
                output.item == null)
            {
                // Drop off item
                output_selector_item.transform.SetParent(null);
                output.item = output_selector_item;
                output_selector_item = null;

                // Cycle to next output
                current_output = (current_output + 1) % outputs.Count;
            }

        // Move input selector item along input selector
        if (input_selector_item != null)
            if (move_towards(input_selector_item.transform,
                input_selector.position, 1f))
            {
                // Transfer input to output selector
                if (output_selector_item == null)
                {
                    output_selector_item = input_selector_item;
                    output_selector_item.transform.SetParent(output_selector);
                    input_selector_item = null;
                }
            }
    }
}
