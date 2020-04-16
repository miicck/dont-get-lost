using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class harvestable : interactable
{
    public string custom_cursor = null;
    public override string cursor()
    {
        if (custom_cursor == null || custom_cursor == "")
            return base.cursor();
        return custom_cursor;
    }

    public override void on_start_interaction(
        RaycastHit point_hit, item interact_with, INTERACT_TYPE type)
    {
        // Add the object to the player inventory, the stop the interaction
        if (player.current.inventory.add(product, 1))
            popup_message.create("Harvested " + product);
        stop_interaction();
    }

    public string product = "log";
}