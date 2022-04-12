using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class restricted_item_slot : inventory_slot
{
    public List<item> acceptable_items;

    public override bool accepts(item item)
    {
        if (item == null)
            return false;
        foreach (var i in acceptable_items)
            if (i.name == item.name)
                return true;
        return false;
    }
}
