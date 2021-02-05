using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fuel_requirement : ingredient
{
    /// <summary> The amount of fuel required. </summary>
    public int fuel_required = 1;

    public override float average_value()
    {
        var log = Resources.Load<item>("items/log");
        return (float)(log.value * fuel_required) / log.fuel_value;
    }

    bool find(
        IItemCollection i,
        ref Dictionary<string, int> in_use,
        out int satisfaction)
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

            // Less than itm.fuel_value required; use anyway
            if (required == 0 && remaining > 0)
                required = 1;

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
                in_use[itm.name] = kv.Value + already_in_use;
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

        satisfaction = Mathf.Min(fuel_required, fuel_required - remaining);
        return remaining <= 0;
    }

    /// <summary> If there is enough fuel in the given inventory to satisfy the
    /// given total fuel requirement, return a dictionary of string:int of items
    /// and quantities needed to satisfy that requirement. Otherwise return
    /// null. </summary>
    public override bool find(
        IItemCollection i,
        ref Dictionary<string, int> in_use)
    {
        return find(i, ref in_use, out int ignored);
    }

    public override string satisfaction_string(
        IItemCollection i,
        ref Dictionary<string, int> in_use)
    {
        find(i, ref in_use, out int satisfaction);
        return satisfaction + "/" + fuel_required + " units of fuel";
    }

    public override string str()
    {
        if (fuel_required == 1) return "A single unit of fuel";
        return fuel_required + " units of fuel";
    }
}
