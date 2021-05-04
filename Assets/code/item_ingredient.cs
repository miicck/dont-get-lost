using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_ingredient : ingredient
{
    public item item;
    public int count;

    public override float average_value()
    {
        return item.value * count;
    }

    public override string str()
    {
        if (item == null)
            throw new System.Exception("Item ingredient missing on " + name);

        if (count > 1) return count + " " + item.plural;
        return item.display_name;
    }

    bool find(
        IItemCollection i,
        ref Dictionary<string, int> in_use,
        out int found)
    {
        if (item == null)
            throw new System.Exception("Item ingredient missing!");

        if (count < 1)
            throw new System.Exception("Item ingredient count must be >= 1!");

        // Ensure the inventory contains enough of 
        // the item to satisfy the already in_use 
        // items and those required by me.
        if (!in_use.TryGetValue(item.name, out int already_in_use))
            already_in_use = 0;

        int in_inventory = i.count(item); // How many are in the inventory, in total
        int spare = Mathf.Max(0, in_inventory - already_in_use); // How many are spare in the inventory
        found = Mathf.Min(spare, count); // How many we've found to satisfy this requirement

        // Update the in_use dictionary
        if (in_use.ContainsKey(item.name)) in_use[item.name] += found;
        else if (found > 0) in_use[item.name] = found;

        // Satisfied or not
        return found >= count;
    }

    public override bool find(
        IItemCollection i,
        ref Dictionary<string, int> in_use)
    {
        return find(i, ref in_use, out int ignored);
    }

    public override string satisfaction_string(IItemCollection i, ref Dictionary<string, int> in_use)
    {
        find(i, ref in_use, out int found);
        return found + "/" + count + " " + (count > 0 ? item.plural : item.display_name);
    }
}