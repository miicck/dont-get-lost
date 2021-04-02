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

    public override INTERACTION_RESULT on_assign(settler s)
    {
        found_element = town_path_element.nearest_element(transform.position);
        if (found_element == null || found_element.group != s.group)
            return INTERACTION_RESULT.FAILED;
        transform.position = found_element.transform.position;
        return INTERACTION_RESULT.UNDERWAY;
    }
}