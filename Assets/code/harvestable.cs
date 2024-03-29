﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be harvested with a specific tool, 
/// to yield a specific product, repeatedly. </summary>
[RequireComponent(typeof(item_product))]
[RequireComponent(typeof(item_requirement))]
public class harvestable : accepts_item_impact, IPlayerInteractable
{
    public item_product[] products { get => GetComponents<item_product>(); }
    public item_requirement tool { get => GetComponent<item_requirement>(); }

    public override bool on_impact(item i)
    {
        if (tool.satisfied(i))
        {
            int count = tool.oversatisfaction(i) + 1;
            foreach (var p in products)
                p.create_in(player.current.inventory, count: count);
            return true;
        }
        return false;
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[]
        {
            new player_inspectable(transform)
            {
                text = ()=> item_product.product_plurals_list(products) + " can be harvested with " +
                            utils.a_or_an(tool.display_name) + " " + tool.display_name +
                            " (higher quality tools will produce more products).",
                sprite = ()=> products[0].sprite
            }
        };
    }
}