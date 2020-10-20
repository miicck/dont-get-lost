using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character
{
    public override bool persistant()
    {
        return true;
    }

    protected override ICharacterController default_controller()
    {
        return new settler_control();
    }
}

class settler_control : ICharacterController
{
    List<settler_path_element> path;
    settler_interactable target;
    HashSet<settler_interactable> unpathable_from_target = new HashSet<settler_interactable>();
    float walk_speed_mod = 1f;
    bool interaction_complete = false;

    public void control(character c)
    {
        if (target == null)
        {
            // Set the initial target to the nearest one and teleport there
            target = settler_interactable.nearest(settler_interactable.TYPE.WORK, c.transform.position);
            c.transform.position = target.path_element.transform.position;
        }

        if (path != null && path.Count > 0)
        {
            // Walk the path to completion  
            Vector3 next_point = path[0].transform.position;
            Vector3 forward = next_point - c.transform.position;
            forward.y = 0;
            if (forward.magnitude > 10e-3f) c.transform.forward = forward;

            if (utils.move_towards(c.transform, next_point,
                Time.deltaTime * c.walk_speed * walk_speed_mod))
                path.RemoveAt(0);

            return;
        }

        if (interaction_complete || target.interact())
        {
            interaction_complete = true;

            // A random interactable object
            var next = settler_interactable.random(settler_interactable.TYPE.WORK);

            // Don't consider unpathable objects, or returning to the same target
            if (unpathable_from_target.Contains(next)) return;
            if (next == target) return;

            // Attempt to path to new target
            path = settler_path_element.path(target.path_element, next.path_element);

            if (path == null)
            {
                // Pathing failed, record this
                unpathable_from_target.Add(next);
                return;
            }

            // Pathing success, this is our next target
            target = next;
            interaction_complete = false;

            // Set a new randomized path speed, so settlers 
            // don't end up 100% in-phase
            walk_speed_mod = Random.Range(0.9f, 1.1f);
        }
    }

    public void draw_gizmos()
    {
        Gizmos.color = Color.green;

        if (target != null)
            Gizmos.DrawWireSphere(target.transform.position, 0.2f);

        if (path != null)
            for (int i = 1; i < path.Count; ++i)
                Gizmos.DrawLine(
                    path[i].transform.position + Vector3.up,
                    path[i - 1].transform.position + Vector3.up);
    }

    public void draw_inspector_gui() { }
}
