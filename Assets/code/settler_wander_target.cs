using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_wander_target : settler_interactable
{
    public override town_path_element path_element(int group)
    {
        return found_element;
    }

    town_path_element found_element;

    protected override bool ready_to_assign(settler s)
    {
        found_element = town_path_element.nearest_element(transform.position);
        if (found_element == null || found_element.group != s.group)
            return false;
        return true;
    }

    protected override void on_assign(settler s)
    {
        transform.position = found_element.transform.position;
    }

    public override string task_summary()
    {
        return "wandering";
    }
}