using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory_quickbar_slot : inventory_slot
{
    protected override void on_change()
    {
        base.on_change();
        player.current.validate_equip();
    }
}
