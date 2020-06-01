using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object_harvested : world_object_modifier
{
    protected override void change_target(world_object old_target, world_object new_target)
    {
        if (old_target != null)
            foreach (var p in old_target.GetComponentsInChildren<harvest_by_hand>(true))
                p.gameObject.SetActive(true);

        if (new_target != null)
            foreach (var p in new_target.GetComponentsInChildren<harvest_by_hand>(true))
                p.gameObject.SetActive(false);
    }
}
