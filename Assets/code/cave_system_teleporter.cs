using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cave_system_teleporter : MonoBehaviour, IAcceptLeftClick
{
    public bool is_underground => transform.position.y < world.UNDERGROUND_ROOF;
    public Transform teleport_spot;

    public void on_left_click()
    {
        var target = utils.find_to_min(FindObjectsOfType<cave_system_teleporter>(), (t) =>
        {
            // Has to be of the opposite type
            if (t.is_underground == is_underground)
                return Mathf.Infinity;

            // Find the nearest in the x-z plane
            Vector3 delta = (transform.position - t.transform.position);
            delta.y = 0;
            return delta.sqrMagnitude;
        });

        if (target == null)
        {
            Debug.Log("Cave teleport target is null, has the world loaded?");
            return;
        }

        player.current.teleport(target.teleport_spot.position);
    }

    public string left_click_context_tip()
    {
        if (is_underground) return "Left click to leave cave";
        return "Left click to enter cave";
    }
}
