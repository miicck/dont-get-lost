using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_hitbox : accepts_item_impact
{
    player player;
    void Start()
    {
        player = GetComponent<player>();
    }

    public override bool on_impact(item i)
    {
        if (i is melee_weapon)
        {
            var mw = (melee_weapon)i;
            player.take_damage(mw.damage);
        }
        return true;
    }
}
