using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class has_path_elements : MonoBehaviour
{
    public town_path_element[] path_elements
    {
        get => GetComponentsInChildren<town_path_element>();
    }

    /// <summary> Returns the path element that a settler from the
    /// given group can use to access this interactable </summary>
    public virtual town_path_element path_element(int group)
    {
        foreach (var e in path_elements)
            if (e.group == group)
                return e;
        return null;
    }
}

/*
/// <summary> An interaction dispensed by a <see cref="settler_interactable"/>. </summary>
public abstract class settler_interaction
{

}
*/

public class settler_interactable : has_path_elements, INonBlueprintable, INonEquipable, IAddsToInspectionText
{
    /// <summary> Returns the networked object to which I belong. </summary>
    public networked networked_parent => GetComponentInParent<networked>();

    /// <summary> The network ID of my parent (or -1 if I have none) - used to identify me. </summary>
    public int network_id => networked_parent == null ? -1 : networked_parent.network_id;

    /// <summary> The type of interaction this is. This will determine when 
    /// a settler decides to use this interactable object. </summary>
    public skill skill;

    /// <summary> The maximum number of settlers that 
    /// can be assigned to this interactable. </summary>
    public int max_simultaneous_users = 1;

    protected virtual void Start()
    {
        register_interactable(this);
    }

    protected virtual void OnDestroy()
    {
        forget_interactable(this);
    }

    public enum INTERACTION_RESULT
    {
        COMPLETE,
        FAILED,
        UNDERWAY,
    }

    public virtual bool ready_to_assign(settler s) { return true; }
    public virtual INTERACTION_RESULT on_assign(settler s) { return INTERACTION_RESULT.COMPLETE; }
    public virtual INTERACTION_RESULT on_interact(settler s) { return INTERACTION_RESULT.COMPLETE; }
    public virtual void on_unassign(settler s) { }

    public virtual string task_info() { return name; }
    public virtual float move_to_speed(settler s) { return s.walk_speed; }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public virtual string added_inspection_text()
    {
        if (skill == null) return "Required skill type undetermined.";
        if (!skill.is_visible) return null;
        return "Requires the " + skill.display_name + " skill.";
    }

    //##############//
    // STATIC STUFF //
    //##############//

    // All interactions
    static HashSet<settler_interactable> interactables = new HashSet<settler_interactable>();

    public static settler_interactable random(skill s)
    {
        var possibilities = new List<settler_interactable>();
        foreach (var i in interactables)
            if (i.skill == s)
                possibilities.Add(i);

        if (possibilities.Count == 0) return null;
        return possibilities[Random.Range(0, possibilities.Count)];
    }

    public static settler_interactable try_find_by_id(int network_id)
    {
        if (network_id <= 0) return null;
        return networked.try_find_by_id(network_id)?.GetComponentInChildren<settler_interactable>();
    }

    static void register_interactable(settler_interactable i)
    {
        interactables.Add(i);
    }

    static void forget_interactable(settler_interactable i)
    {
        interactables.Remove(i);
    }

    public static string info()
    {
        return "Total interactions : " + interactables.Count;
    }
}
