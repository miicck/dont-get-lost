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

    public virtual INTERACTION_RESULT on_arrive(visiting_character visitor) => INTERACTION_RESULT.UNDERWAY;
    public virtual INTERACTION_RESULT interact(visiting_character visitor) => INTERACTION_RESULT.COMPLETE;
    public virtual void on_leave(visiting_character visitor) { }

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

public abstract class town_element_point_of_interest : visitor_point_of_interest
{
    private void Start()
    {
        if (target_element == null)
            Debug.LogError(name + " must have a path element to be found by visitors!");
    }

    public sealed override town_path_element target_element => GetComponent<town_path_element>();
}

class explore_point_of_interest : visitor_point_of_interest
{
    public town_path_element element;
    public override town_path_element target_element => element;
    public override string description() => "Exploring town.";
}