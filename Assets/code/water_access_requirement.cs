using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class building_operation_requirement : MonoBehaviour
{
    public abstract bool satisfied(out string reason);

    public static bool all_operation_requirements_satisfied(Component b, out string reason)
    {
        reason = "No problems";
        foreach (var r in b.GetComponentsInChildren<building_operation_requirement>())
            if (!r.satisfied(out reason))
                return false;
        return true;
    }
}

public class water_access_requirement : building_operation_requirement
{
    public float max_distance = 1f;

    public override bool satisfied(out string reason)
    {
        if (transform.position.y < world.SEA_LEVEL)
        {
            reason = "No water access (underwater)";
            return false; // Underwater
        }

        if (transform.position.y > world.SEA_LEVEL + max_distance)
        {
            reason = "No water access (too far from water)";
            return false;
        }

        var c = utils.raycast_for_closest<Collider>(new Ray(transform.position, Vector3.down), out RaycastHit hit);

        if (c == null)
        {
            reason = "No water access (underground)";
            return false;
        }

        if (hit.point.y > world.SEA_LEVEL)
        {
            reason = "Water access blocked";
            return false;
        }

        reason = null;
        return true;
    }
}
