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
        character.play_random_sound(character_sound.TYPE.INJURY);
        if (i is melee_weapon)
        {
            var mw = (melee_weapon)i;
            character.health.value -= mw.damage;
        }
        return true;
    }
}
