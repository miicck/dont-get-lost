using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cave_system_teleporter : MonoBehaviour, IPlayerInteractable
{
    public bool is_underground => transform.position.y < world.UNDERGROUND_ROOF;
    public Transform teleport_spot;

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] { new interaction(this) };
    }

    class interaction : player_interaction
    {
        cave_system_teleporter teleporter;
        public interaction(cave_system_teleporter teleporter) { this.teleporter = teleporter; }

        public override controls.BIND keybind => controls.BIND.ENTER_EXIT_CAVE;

        public override string context_tip()
        {
            if (teleporter.is_underground) return "return to the surface";
            return "go underground";
        }

        protected override bool on_start_interaction(player player)
        {
            var target = utils.find_to_min(FindObjectsOfType<cave_system_teleporter>(), (t) =>
            {
                // Has to be of the opposite type
                if (t.is_underground == teleporter.is_underground)
                    return Mathf.Infinity;

                // Find the nearest in the x-z plane
                Vector3 delta = (teleporter.transform.position - t.transform.position);
                delta.y = 0;
                return delta.sqrMagnitude;
            });

            if (target == null)
            {
                Debug.Log("Cave teleport target is null, has the world loaded?");
                return true;
            }

            player.current.teleport(target.teleport_spot.position);
            return true;
        }
    }
}