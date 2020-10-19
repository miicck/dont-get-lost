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
    settler_path_element current_road;
    settler_path_element next_road;
    float progress = 0;

    public void control(character c)
    {
        if (current_road == null)
        {
            current_road = settler_path_element.find_nearest(c.transform.position);
            if (current_road != null)
                c.transform.position = current_road.transform.position;
        }
        else
        {
            if (next_road == null)
            {
                var rds = current_road.linked_elements();
                if (rds.Count == 0) return;
                next_road = rds[Random.Range(0, rds.Count)];
            }

            progress += Time.deltaTime;
            if (progress > 1f)
            {
                progress = 0f;
                current_road = next_road;
                next_road = null;
                return;
            }

            c.transform.position = Vector3.Lerp(
                current_road.transform.position, 
                next_road.transform.position, progress);

            Vector3 forward = next_road.transform.position -
                current_road.transform.position;
            forward.y = 0;
            c.transform.forward = forward;
        }
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
}
