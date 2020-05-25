using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class character_hitbox : accepts_item_impact
{
    public override bool on_impact(item i)
    {
        return base.on_impact(i);
    }
}
