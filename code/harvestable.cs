using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class harvestable : accepts_item_impact
{
    public string product = "log";
    public string tool = "axe";

    public override bool on_impact(item i)
    {
        if (i.name == tool)
        {
            player.current.inventory.add(product, 1);
            return true;
        }
        return false;
    }
}