using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dynamite : item
{
    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        var ray = player.current.camera_ray(player.INTERACTION_RANGE, out float dis);

        if (Physics.Raycast(ray, out RaycastHit hit, dis))
        {
            var wo = hit.collider.GetComponentInParent<world_object>();
            if (wo != null)
            {
                var wod = (world_object_destroyed)client.create(
                    wo.transform.position, "misc/world_object_destroyed");
                wod.target_to_world_object(wo);
            }
        }

        return use_result.complete;
    }
}
