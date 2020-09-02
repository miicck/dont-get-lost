using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : building_material, IInspectable
{
    public string recipes_folder;
    item_link_point[] inputs;
    item_link_point[] outputs;
    recipe[] recipies;

    simple_item_collection pending_inputs = new simple_item_collection();
    simple_item_collection pending_outputs = new simple_item_collection();

    public override string inspect_info()
    {
        string info = display_name + "\n";

        var pi = pending_inputs.contents();
        var po = pending_outputs.contents();

        if (pi.Count > 0)
        {
            info += "Pending inputs:\n";
            foreach (var kv in pi)
                info += "    " + kv.Value + " " +
                    kv.Key.singular_or_plural(kv.Value) + "\n";

        }

        if (po.Count > 0)
        {
            info += "Pending outputs:\n";
            foreach (var kv in po)
                info += "    " + kv.Value + " " +
                    kv.Key.singular_or_plural(kv.Value) + "\n";
        }

        return info;
    }

    void Start()
    {
        // Get references to input/output link points
        List<item_link_point> inputs = new List<item_link_point>();
        List<item_link_point> outputs = new List<item_link_point>();
        recipies = Resources.LoadAll<recipe>(recipes_folder);

        foreach (var lp in GetComponentsInChildren<item_link_point>())
        {
            if (lp.type == item_link_point.TYPE.INPUT)
                inputs.Add(lp);
            else if (lp.type == item_link_point.TYPE.OUTPUT)
                outputs.Add(lp);
            else
                throw new System.Exception("Unkown link type!");
        }

        this.inputs = inputs.ToArray();
        this.outputs = outputs.ToArray();
    }

    void Update()
    {
        // Add inputs to the pending inputs collection
        bool inputs_changed = false;
        foreach (var ip in inputs)
            if (ip.item != null)
            {
                pending_inputs.add(ip.item, 1);
                ip.delete_item();
                inputs_changed = true;
            }

        // Output stuff from the pending outputs collection
        bool outputs_free = true;
        foreach (var op in outputs)
        {
            if (op.item == null)
            {
                var first = pending_outputs.remove_first();
                if (first != null)
                {
                    // Create a client-side version of the product
                    op.item = create(first.name,
                        op.position, op.transform.rotation);
                    outputs_free = false;
                }
            }
            else outputs_free = false;
        }

        if (inputs_changed && outputs_free)
        {
            // Attempt to craft something
            foreach (var r in recipies)
                if (r.craft(pending_inputs, pending_outputs))
                {
                    pending_inputs.clear();
                    break;
                }
        }
    }
}
