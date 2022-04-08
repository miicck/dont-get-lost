using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class visitor_point_of_interest : MonoBehaviour
{
    public enum INTERACTION_RESULT
    {
        COMPLETE,
        FAILED,
        UNDERWAY
    }

    public abstract town_path_element target_element { get; }
    public abstract INTERACTION_RESULT interact(visiting_character visitor);
    public abstract string description();

    public static List<visitor_point_of_interest> all_in_group(int group)
    {
        List<visitor_point_of_interest> ret = new List<visitor_point_of_interest>();
        foreach (var e in town_path_element.element_group(group))
        {
            var poi = e.GetComponent<visitor_point_of_interest>();
            if (poi != null)
                ret.Add(poi);
        }
        return ret;
    }
}

class explore_point_of_interest : visitor_point_of_interest
{
    public town_path_element element;
    public override town_path_element target_element => element;
    public override INTERACTION_RESULT interact(visiting_character visitor) => INTERACTION_RESULT.COMPLETE;
    public override string description() => "Exploring town.";
}