using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A component to link a chest to an output such that items
/// will only be released if whatever the output is connected
/// to is free. </summary>
public class buffer_chest : MonoBehaviour
{
    public chest buffering;
    public item_output output;
    item_dropper dropper;

    private void Start()
    {
        if (buffering == null)
            throw new System.Exception("Buffer chest unassigned!");
    }

    private void Update()
    {
        if (dropper != null) return; // Wait for dropper to finish
        var no = output.peek_next_output();
        if (no == null) return; // No output
        if (no.item_count > 0) return; // Don't release item unless output is free (buffering)
        if (buffering.inventory == null) return; // Wait for inventory to be available

        if (Time.frameCount <= buffering.inventory.last_frame_changed)
            return; // Wait until at least 1 frame after chest input to allow network updates to propagate

        var to_buffer = buffering.has_authority ?
            buffering.inventory.remove_first() :
            buffering.inventory.get_first();

        if (to_buffer == null) return; // Nothing to buffer
        dropper = item_dropper.create(item.create(
            to_buffer.name, output.output_point, output.transform.rotation,
            logistics_version: true), output.output_point, output.next_output());
    }
}