using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : item_logistic_building, IInspectable
{
    public float craft_time = 1f;
    public string recipes_folder;
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

    protected override void Start()
    {
        base.Start();

        // Load the recipes
        recipies = Resources.LoadAll<recipe>(recipes_folder);
        InvokeRepeating("crafting_update", craft_time, craft_time);
    }

    void crafting_update()
    {
        // Add inputs to the pending inputs collection
        bool inputs_changed = false;
        foreach (var ip in item_inputs)
            if (ip.item != null)
            {
                pending_inputs.add(ip.item, 1);
                ip.delete_item();
                inputs_changed = true;
            }

        // Output stuff from the pending outputs collection
        bool outputs_free = true;
        foreach (var op in item_outputs)
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

        if ((inputs_changed || item_inputs.Count == 0) && outputs_free)
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
