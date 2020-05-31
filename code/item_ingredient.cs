using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_ingredient : ingredient
{
    public item item;
    public int count;

    public override string str()
    {
        if (count > 1) return count + " " + item.plural;
        return item.display_name();
    }

    public override bool in_inventory(inventory_section i)
    {
        foreach (var s in i.slots)
            if (s.item == item.name && s.count >= count)
                return true;
        return false;
    }

    public override void on_craft(inventory_section i)
    {
        i.remove(item.name, count);
    }
}
