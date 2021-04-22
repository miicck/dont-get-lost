using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crane_pickup_node : item_node, IAddsToInspectionText
{
    public int capacity = 5;
    public Transform box;
    public Transform box_output;
    public item_input input_node;

    // Reset position information for the box
    public Transform box_reset_parent { get; private set; }
    public Vector3 box_reset_local_pos { get; private set; }
    public Quaternion box_reset_local_rot { get; private set; }

    public bool ready_for_pickup => item_count == capacity;
    public bool box_docked => box.parent == box_reset_parent;

    public override string node_description(int item_count) { return "Crane box contains " + item_count + " items"; }

    //#################//
    // Unity callbacks //
    //#################//

    void set_box_parent()
    {
        // Don't overwrite
        if (box_reset_parent != null) return;

        // Remember where the box should be returned to
        box_reset_parent = box.parent;
        box_reset_local_pos = box.localPosition;
        box_reset_local_rot = box.localRotation;
    }

    protected override void Start()
    {
        base.Start();
        set_box_parent();
    }

    private void Update()
    {
        if (input_node.item_count == 0) return; // No pending items
        if (item_count >= capacity) return; // Full
        if (!box_docked) return; // Box is away

        // Accept the next pending input
        add_item(input_node.release_item(0));
    }

    protected override void OnDestroy()
    {
        // Ensure the box is destroyed along with the pickup node
        set_box_parent();
        if (!box_docked) Destroy(box.gameObject);
        base.OnDestroy();
    }

    //###########//
    // item_node //
    //###########//

    protected override bool on_add_item(item i)
    {
        // Make added items invisible
        i.gameObject.SetActive(false);
        return true;
    }

    protected override bool can_output_to(item_node other)
    {
        // Can only output to crane_nodes
        if (other == null) return false;
        return other is crane_node;
    }

    protected override bool can_input_from(item_node other)
    {
        // No direct inputs allowed (input comes from input_node)
        return false;
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    new public string added_inspection_text()
    {
        return "Contains " + item_count + "/" + capacity + " items";
    }
}
