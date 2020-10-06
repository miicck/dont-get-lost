using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object_harvested : world_object_modifier
{
    protected override void change_target(world_object old_target, world_object new_target)
    {
        // Toggle harvest_by_hand objects enabled state
        if (old_target != null)
            foreach (var h in old_target.GetComponentsInChildren<harvest_by_hand>(true))
                h.gameObject.SetActive(true);

        if (new_target != null)
            foreach (var h in new_target.GetComponentsInChildren<harvest_by_hand>(true))
                h.gameObject.SetActive(false);
    }
}