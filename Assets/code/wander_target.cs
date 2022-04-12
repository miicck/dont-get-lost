using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wander_target : character_interactable
{
    town_path_element found_element;

    protected override bool ready_to_assign(character c)
    {
        found_element = town_path_element.nearest_element(transform.position);
        if (found_element == null || found_element.group != c.group)
            return false;
        return true;
    }

    public override town_path_element path_element(int group) => found_element;
    protected override void on_assign(character c) => transform.position = found_element.transform.position;
    public override string task_summary() => "wandering";
}