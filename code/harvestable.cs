using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be harvested with a specific tool, 
/// to yield a specific product, repeatedly. </summary>
public class harvestable : accepts_item_impact, IInspectable
{
    public product product;
    public melee_weapon tool;

    public override bool on_impact(item i)
    {
        if (i.name == tool.name)
        {
            product.create_in_inventory(player.current.inventory.contents);
            return true;
        }
        return false;
    }

    public string inspect_info()
    {
        return product.product_name_plural() + " can be harvested with " +
               utils.a_or_an(tool.display_name) + " " + tool.display_name;
    }

    public Sprite main_sprite()
    {
        return product.sprite();
    }

    public Sprite secondary_sprite()
    {
        return tool.sprite;
    }
}