using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(character))]
public class character_hitbox : accepts_item_impact
{
    character character;
    private void Start()
    {
        character = GetComponent<character>();
    }

    public override bool on_impact(item i)
    {
        if (i is melee_weapon)
        {
            var mw = (melee_weapon)i;
            character.take_damage(mw.damage);
        }
        return true;
    }
}
