using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be harvested with a specific tool, 
/// to yield a specific product, repeatedly. </summary>
public class harvestable : accepts_item_impact, IInspectable
{
    public string product = "log";
    public string tool = "axe";

    item product_item { get => Resources.Load<item>("items/" + product); }
    item tool_item { get => Resources.Load<item>("items/" + tool); }

    public override bool on_impact(item i)
    {
        if (i.name == tool)
        {
            player.current.inventory.add(product, 1);
            return true;
        }
        return false;
    }

    public string inspect_info()
    {
        return product_item.plural + " can be harvested with " + 
               utils.a_or_an(tool) + " " + tool;
    }

    public Sprite main_sprite()
    {
        return product_item.sprite;
    }

    public Sprite secondary_sprite()
    {
        return tool_item.sprite;
    }
}