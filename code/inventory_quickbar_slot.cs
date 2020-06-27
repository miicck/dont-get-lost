using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory_quickbar_slot : inventory_slot
{
    public override void update(item item, int count, inventory inventory)
    {
        base.update(item, count, inventory);
        inventory.GetComponentInParent<player>()?.validate_equip();
    }
}
