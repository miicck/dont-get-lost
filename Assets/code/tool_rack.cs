using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tool_rack : walk_to_settler_interactable
{
    public item_input input;
    public item_output output;
    public List<Transform> slots;

    Vector3 input_point => input.output_point;
    Vector3 output_point => output.input_point(input.output_point);

    List<Transform> slot_junctions = new List<Transform>();
    List<item> output_queue = new List<item>();

    //#################//
    // UNITY callbacks //
    //#################//

    protected override void Start()
    {
        base.Start();
        foreach (var s in slots)
        {
            // Create a transform at the slot junction 
            var sj = new GameObject("slot_junction").transform;
            sj.transform.SetParent(transform);
            sj.transform.localRotation = Quaternion.identity;
            sj.transform.position = utils.nearest_point_on_line(s.position, input_point, output_point - input_point);
            slot_junctions.Add(sj);
        }
    }

    private void Update()
    {
        // Accept inputs
        if (input.item_count > 0)
            foreach (var i in input.relesae_all_items())
            {
                i.transform.SetParent(slot_junctions[0]);
                i.transform.position = input_point;
                Vector3 fw = output_point - input_point;
                fw.y = 0;
                i.transform.forward = fw;
            }

        // Move things along the junctions
        for (int i = 0; i < slot_junctions.Count; ++i)
        {
            var s = slot_junctions[i];
            foreach (Transform c in s.transform)
                if (utils.move_towards(c, s.position + offset(c), Time.deltaTime))
                {
                    // Transfer to the slot if there is space
                    if (c.GetComponent<tool>() != null && slots[i].childCount == 0)
                        c.SetParent(slots[i]);

                    // Transfer to the next slot if there is one
                    else if (i < slot_junctions.Count - 1)
                        c.SetParent(slot_junctions[i + 1]);

                    // Transfer to output
                    else
                    {
                        c.SetParent(transform);
                        output_queue.Add(c.GetComponent<item>());
                    }
                }
        }

        // Move things into slots
        foreach (var s in slots)
            foreach (Transform c in s)
                utils.move_towards(c, s.position + offset(c), Time.deltaTime);

        // Move things towards output
        for (int i = output_queue.Count - 1; i >= 0; --i)
        {
            var itm = output_queue[i];
            if (utils.move_towards(itm.transform, output_point + offset(itm.transform), Time.deltaTime))
            {
                output.add_item(itm);
                output_queue.RemoveAt(i);
            }
        }
    }

    Vector3 offset(Transform t)
    {
        var tool = t.GetComponent<tool>();
        if (tool == null) return Vector3.zero;
        return tool.hanging_point_offset;
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (input == null || output == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(input_point, output_point);

        for (int i = 0; i < slot_junctions.Count; ++i)
            Gizmos.DrawLine(slot_junctions[i].position, slots[i].position);
    }

    //##############################//
    // walk_to_settler_interactable //
    //##############################//

    public override string task_summary()
    {
        return "Getting tools";
    }

    Dictionary<tool.TYPE, tool> tools_needed_by(settler set)
    {
        var ret = new Dictionary<tool.TYPE, tool>();

        foreach (var s in slots)
            foreach (Transform c in s.transform)
            {
                var t = c.GetComponent<tool>();
                if (t != null) ret[t.type] = t;
            }

        foreach (var kv in set.inventory.contents())
            if (kv.Key is tool)
                ret.Remove(((tool)kv.Key).type);

        return ret;
    }

    protected override bool ready_to_assign(settler s)
    {
        if (s.inventory == null) return false;
        return tools_needed_by(s).Count > 0;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        foreach (var kv in tools_needed_by(s))
        {
            s.inventory.add(kv.Value, 1);
            Destroy(kv.Value.gameObject);
        }
        return STAGE_RESULT.TASK_COMPLETE;
    }
}
