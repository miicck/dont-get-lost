using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pick_by_hand : MonoBehaviour
{
    public float regrow_time = 1f;
    public string item_to_pick = "lettuce";
    public int quantity_to_pick = 1;

    public void on_pick()
    {
        var wo = GetComponentInParent<world_object>();

        var woh = (world_object_harvested)
            client.create(wo.transform.position, 
            "misc/world_object_harvested");

        woh.x_in_chunk.value = wo.x_in_chunk;
        woh.z_in_chunk.value = wo.z_in_chunk;
        woh.timeout.value = regrow_time;

        player.current.inventory.add(item_to_pick, quantity_to_pick);
    }
}