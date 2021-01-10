using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be harvested with a specific tool, 
/// to yield a specific product, repeatedly. </summary>
[RequireComponent(typeof(product))]
[RequireComponent(typeof(item_requirement))]
public class harvestable : accepts_item_impact, IPlayerInteractable
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

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions()
    {
        return new player_interaction[]
        {
            new player_inspectable(transform)
            {
                text = ()=> product.product_plurals_list(products) + " can be harvested with " +
                            utils.a_or_an(tool.display_name) + " " + tool.display_name + ".",
                sprite = ()=> products[0].sprite()
            }
        };
    }
}