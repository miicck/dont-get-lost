using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character
{
    public float hunger
    {
        get => _hunger;
        set => _hunger = Mathf.Clamp(value, 0, 100);
    }
    float _hunger;

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
    float walk_speed_mod = 1f;
    bool interaction_complete = false;
    float interaction_time = 0;

    public void control(character c)
    {
        if (target == null)
        {
            // Set the initial target to the nearest one and teleport there
            target = settler_interactable.nearest(settler_interactable.TYPE.WORK, c.transform.position);
            if (target == null || target.path_element == null) return;
            c.transform.position = target.path_element.transform.position;
        }

        if (path != null && path.Count > 0)
        {
            if (path[0] == null)
            {
                // Path has been destroyed, reset
                path = null;
                target = null;
                return;
            }

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

        if (interaction_complete || target.interact((settler)c, interaction_time))
        {
            interaction_complete = true;

            // A random interactable object
            var next = settler_interactable.random();

            // Don't consider null interactables
            // or returning to the same target
            if (next == null) return;
            if (next == target) return;

            // Attempt to path to new target
            path = settler_path_element.path(target.path_element, next.path_element);

            if (path == null)
            {
                // Pathing failed
                return;
            }

            // Pathing success, this is our next target
            target = next;

            // Reset things
            interaction_complete = false;
            interaction_time = 0;

            // Set a new randomized path speed, so settlers 
            // don't end up 100% in-phase
            walk_speed_mod = Random.Range(0.9f, 1.1f);
        }
        else
        {
            // Increment the interaction timer
            interaction_time += Time.deltaTime;
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

    public void draw_inspector_gui()
    {
#if UNITY_EDITOR
        string text = target == null ? "No target\n" : "Target = " + target.name + "\n";
        text += (path == null) ? "No path\n" : "Path length = " + path.Count;
        UnityEditor.EditorGUILayout.TextArea(text);
#endif
    }
}
