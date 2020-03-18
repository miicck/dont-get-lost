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

    public override void on_start_interaction(RaycastHit point_hit)
    {
        // Immediately spawn the object, then stop the interaction
        Vector3 spawn_point = player.current.camera.transform.position +
                      player.current.camera.transform.forward;
        item.spawn(product, spawn_point);
        stop_interaction();
    }

    public string product = "log";
}