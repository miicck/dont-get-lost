using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift_bucket : MonoBehaviour
{
    float last_sign = 1f;
    public item_input input;
    public item_output output;

    item item
    {
        get => _item;
        set
        {
            if (_item != null)
                throw new System.Exception("Tried to overwrite item!");
            _item = value;
            if (_item != null)
            {
                _item.transform.SetParent(transform);
                _item.transform.localPosition = Vector3.zero;
            }
        }
    }
    item _item;

    item release_item()
    {
        var tmp = _item;
        if (_item != null)
            _item.transform.SetParent(null);
        _item = null;
        return tmp;
    }

    private void Update()
    {
        // Check if my y component has changed sign
        if (Mathf.Sign(transform.up.y * last_sign) < 0)
        {
            // Depending on the new sign, either pickup or drop off an item
            last_sign = Mathf.Sign(transform.up.y);
            if (last_sign > 0) request_item_pickup();
            else request_item_offload();
        }
    }

    void request_item_pickup()
    {
        // Pick up an item if we don't already have one
        // and there is one to pick up
        if (item != null) return;
        item = input.release_next_item();
    }

    void request_item_offload()
    {
        // Offload an item to the output if we have
        // one and the output is free
        if (item == null) return;
        output.add_item(release_item());
    }
}
