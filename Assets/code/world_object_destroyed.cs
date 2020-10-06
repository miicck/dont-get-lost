using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object_destroyed : world_object_modifier
{
    protected override void change_target(world_object old_target, world_object new_target)
    {
        if (old_target != null)
            old_target.gameObject.SetActive(true);
        if (new_target != null)
            new_target.gameObject.SetActive(false);
    }
}
