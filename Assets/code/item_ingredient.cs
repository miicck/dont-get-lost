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
            throw new System.Exception("Item ingredient missing on " + name);

        if (count > 1) return count + " " + item.plural;
        return item.display_name;
    }

    public override bool find(
        IItemCollection i,
        ref Dictionary<string, int> in_use)
    {
        if (item == null)
            throw new System.Exception("Item ingredient missing!");

        if (count < 1)
            throw new System.Exception("Item ingredient count must be >= 1!");

        // Ensure the inventory contains enough of 
        // the item to satisfy the already in_use 
        // items and those required by me.
        if (!in_use.TryGetValue(item.name, out int to_find))
            to_find = 0;
        to_find += count;

        if (!i.contains(item, to_find))
            return false;

        if (in_use.ContainsKey(item.name))
            in_use[item.name] += count;
        else
            in_use[item.name] = count;

        return true;
    }
}
