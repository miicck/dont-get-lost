using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be harvested with a specific tool, 
/// to yield a specific product, repeatedly. </summary>
[RequireComponent(typeof(product))]
[RequireComponent(typeof(item_requirement))]
public class harvestable : accepts_item_impact, IInspectable
{
    public product[] products { get => GetComponents<product>(); }
    public item_requirement tool { get => GetComponent<item_requirement>(); }

    public override bool on_impact(item i)
    {
        if (tool.satisfied(i))
        {
            foreach (var p in products)
                p.create_in(player.current.inventory);
            return true;
        }
        return false;
    }

    public string inspect_info()
    {
        return product.product_list(products) + " can be harvested with " +
               utils.a_or_an(tool.display_name) + " " + tool.display_name + ".";
    }

    public Sprite main_sprite()
    {
        return products[0].sprite();
    }

    public Sprite secondary_sprite()
    {
        return tool.sprite;
    }
}