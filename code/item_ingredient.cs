using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_ingredient : ingredient
{
    public item item;
    public int count;

    public override string str()
    {
        if (item == null)
        {
            Debug.Log("Item ingredient missing!");
            return "MISSING";
        }

        if (count > 1) return count + " " + item.plural;
        return item.display_name;
    }

    public override bool in_inventory(inventory_section i)
    {
        if (item == null)
        {
            Debug.Log("Item ingredient missing!");
            return false;
        }

        return i.contains(item, count);
    }

    public override void on_craft(inventory_section i)
    {
        if (item == null)
        {
            Debug.Log("Item ingredient missing!");
            return;
        }

        i.remove(item.name, count);
    }
}
