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
    Dictionary<item, int> find_in_inventory(int fuel_total, inventory i)
    {
        Dictionary<item, int> found = new Dictionary<item, int>();

        int remaining = fuel_total;
        foreach (var kv in i.contents())
        {
            // See if the item is fuel
            var itm = kv.Key;
            if (itm == null) continue;

            if (itm.fuel_value > 0)
            {
                // The amount of this item that would be
                // needed to satisfy the remaining fuel requirement
                int required = remaining / itm.fuel_value;

                if (kv.Value <= required)
                {
                    // Use up all of this item
                    found[itm] = kv.Value;
                    remaining -= itm.fuel_value * kv.Value;
                }
                else
                {
                    // Only use up the required amount of this item
                    found[itm] = required;
                    remaining -= itm.fuel_value * required;
                }

                if (remaining <= 0)
                    break;
            }
        }

        if (remaining <= 0) return found;
        return null;
    }

    public override bool in_inventory(inventory i)
    {
        return find_in_inventory(fuel_required, i) != null;
    }

    public override void on_craft(inventory i)
    {
        var found = find_in_inventory(fuel_required, i);
        if (found == null)
            throw new System.Exception("Tried to burn non-existant fuel!");

        foreach (var kv in found)
            i.remove(kv.Key, kv.Value);
    }

    public override string str()
    {
        if (fuel_required == 1) return "A single unit of fuel";
        return fuel_required + " units of fuel";
    }
}
