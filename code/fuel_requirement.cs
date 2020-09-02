using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fuel_requirement : ingredient
{
    /// <summary> The amount of fuel required. </summary>
    public int fuel_required = 1;

    /// <summary> If there is enough fuel in the given inventory to satisfy the
    /// given total fuel requirement, return a dictionary of string:int of items
    /// and quantities needed to satisfy that requirement. Otherwise return
    /// null. </summary>
    public override bool find(
        IItemCollection i, 
        ref Dictionary<string, int> in_use)
    {
        int remaining = fuel_required;
        foreach (var kv in i.contents())
        {
            // Ensure the item is fuel
            var itm = kv.Key;
            if (itm == null) continue;
            if (itm.fuel_value <= 0) continue;

            // The amount of this item that would be
            // needed to satisfy the remaining fuel requirement
            int required = remaining / itm.fuel_value;

            // The amount of this item that is already in use
            // and how much is available to use as fuel
            if (!in_use.TryGetValue(itm.name, out int already_in_use))
                already_in_use = 0;
            int available = kv.Value - already_in_use;

            if (available <= 0)
            {
                // This item is already fully in use
                continue;
            }
            else if (available <= required)
            {
                // Use up all of this item
                remaining -= itm.fuel_value * available;
                in_use[itm.name] = kv.Value;
            }
            else
            {
                // Only use up the required amount of this item
                remaining -= itm.fuel_value * required;
                in_use[itm.name] = required + already_in_use;
            }

            if (remaining <= 0)
                break;
        }

        return remaining <= 0;
    }

    public override string str()
    {
        if (fuel_required == 1) return "A single unit of fuel";
        return fuel_required + " units of fuel";
    }
}
