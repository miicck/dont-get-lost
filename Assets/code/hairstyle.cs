using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hairstyle : armour_piece
{
    public override void on_equip(ICanEquipArmour entity)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            utils.set_color(r.material, entity.hair_color());
    }
}