using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : building_material, IInspectable
{
    public float craft_time = 1f;
    public string recipes_folder;
    recipe[] recipies;

    simple_item_collection pending_inputs = new simple_item_collection();
    simple_item_collection pending_outputs = new simple_item_collection();

    item_input[] inputs => GetComponentsInChildren<item_input>();
    item_output[] outputs => GetComponentsInChildren<item_output>();

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
        // Load the recipes
        recipies = Resources.LoadAll<recipe>(recipes_folder);
        InvokeRepeating("crafting_update", craft_time, craft_time);
    }

    void crafting_update()
    {
        // Add inputs to the pending inputs collection
        bool inputs_changed = false;
        foreach (var ip in inputs)
            foreach (var itm in ip.relesae_all_items())
            {
                pending_inputs.add(itm, 1);
                Destroy(itm.gameObject);
                inputs_changed = true;
            }

        // Attempt to craft something
        if (inputs_changed)
            foreach (var r in recipies)
                if (r.craft(pending_inputs, pending_outputs))
                {
                    pending_inputs.clear();
                    break;
                }

        int output_number = -1;
        while (true)
        {
            // Nothing to output to
            if (outputs.Length == 0) break;

            // Get the next item to output
            var itm = pending_outputs.remove_first();
            if (itm == null) break; // No items left

            // Cycle items to sequential outputs
            output_number = (output_number + 1) % outputs.Length;
            var op = outputs[output_number];

            op.add_item(create(itm.name,
                        op.transform.position,
                        op.transform.rotation));
        }
    }
}
