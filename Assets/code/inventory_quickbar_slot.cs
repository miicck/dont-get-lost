using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory_quickbar_slot : inventory_slot
{
    public int number;

    public override void update(item item, int count, inventory inventory)
    {
        base.update(item, count, inventory);

        var player = inventory.GetComponentInParent<player>();
        if (player != null && player == player.current)
            toolbar_display_slot.update(number, item, count);
    }
}
