using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : item_proccessor
{
    item_link_point[] inputs;
    item_link_point[] outputs;
    public item product;

    void Start()
    {
        // Get references to input/output link points
        List<item_link_point> inputs = new List<item_link_point>();
        List<item_link_point> outputs = new List<item_link_point>();

        foreach (var lp in link_points)
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
        bool ready = true;
        foreach (var ip in inputs)
            if (ip.item == null)
            {
                ready = false;
                break;
            }

        if (!ready) 
            return;

        // Drop all the inupt items
        foreach(var ip in inputs)
            ip.delete_item();

        // Create the output item
        var created = product.inst();
        created.transform.position = outputs[0].position;
        outputs[0].item = created;
    }
}
