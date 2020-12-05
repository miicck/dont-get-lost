using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift_piston : MonoBehaviour, INonBlueprintable
{
    public Transform bottom;
    public Transform top;
    public item_input input;
    public item_output output;

    item item;

    private void Update()
    {
        if (item == null)
        {
            // Going down
            if (utils.move_towards(transform, bottom.position, Time.deltaTime))
            {
                // Arrived at bottom, attempt to pick up item
                if (input.item_count > 0)
                {
                    item = input.release_next_item();
                    item.transform.SetParent(transform);
                    item.transform.position = transform.position;
                    item.transform.up = Vector3.up;
                }
            }
        }
        else
        {
            // Going up
            if (utils.move_towards(transform, top.position, Time.deltaTime))
            {
                // Arrived at top, drop off item
                output.add_item(item);
                item.transform.SetParent(null);
                item.transform.localScale = Vector3.one * item.logistics_scale;
                item = null;
            }
        }
    }
}
