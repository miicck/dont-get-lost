using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift_piston : MonoBehaviour, INonBlueprintable
{
    public Transform bottom;
    public Transform top;

    item_lift lift => GetComponentInParent<item_lift>();
    item item;

    private void Update()
    {
        if (item == null)
        {
            // Going down
            if (utils.move_towards(transform, bottom.position, Time.deltaTime))
            {
                // Arrived at bottom, attempt to pick up item
                if (lift.input.item != null)
                {
                    item = lift.input.release_item();
                    item.transform.SetParent(transform);
                    item.transform.position = transform.position + 
                        Vector3.up * item_link_point.END_MATCH_DISTANCE / 2f;
                }
            }
        }
        else
        {
            // Going up
            if (utils.move_towards(transform, top.position, Time.deltaTime))
            {
                // Arrived at top, attempt to drop off item
                if (lift.output.item == null)
                {
                    lift.output.item = item;
                    item.transform.SetParent(null);
                    item.transform.localScale = Vector3.one * item.logistics_scale;
                    item = null;
                }
            }
        }
    }
}
