using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class has_path_elements : MonoBehaviour
{
    /// <summary> Returns the path element that a settler from the
    /// given group can use to access this interactable </summary>
    public settler_path_element path_element(int group)
    {
        foreach (var e in GetComponentsInChildren<settler_path_element>())
            if (e.group == group)
                return e;
        return null;
    }
}

public class settler_interactable : has_path_elements, INonBlueprintable, INonEquipable
{
    /// <summary> Returns the networked object to which I belong. </summary>
    public networked networked_parent => GetComponentInParent<networked>();

    /// <summary> Returns the assignments of settlers to this interactable, if they exist. </summary>
    public settler_task_assignment[] assignments => networked_parent?.GetComponentsInChildren<settler_task_assignment>();

    /// <summary> The type of interaction this is. This will determine when 
    /// a settler decides to use this interactable object. </summary>
    public job_type job;

    /// <summary> The maximum number of settlers that 
    /// can be assigned to this interactable. </summary>
    public int max_simultaneous_users = 1;

    bool registered = false;
    protected virtual void Start()
    {
        registered = true;
        register_interactable(this);
    }

    protected virtual void OnDestroy()
    {
        if (registered)
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

    //##############//
    // STATIC STUFF //
    //##############//

    // All interactions, indexed by the default priority of their job type
    static Dictionary<int, List<settler_interactable>> interactables;

    public static settler_interactable nearest(job_type j, Vector3 v)
    {
        return utils.find_to_min(interactables[j.default_priority],
            (i) => (i.transform.position - v).sqrMagnitude);
    }

    public delegate bool accept_func(settler_interactable i);
    public static settler_interactable nearest_acceptable(job_type j, Vector3 v, accept_func f)
    {
        var ret = utils.find_to_min(interactables[j.default_priority], (i) =>
        {
            if (!f(i)) return Mathf.Infinity; // i not acceptable => infinite score
            return (i.transform.position - v).magnitude; // distance = score
        });

        // Only return if acceptable
        if (f(ret)) return ret;
        return null;
    }

    public static settler_interactable proximity_weighted_ramdon(job_type j, Vector3 position)
    {
        var l = interactables[j.default_priority];
        if (l.Count == 0) return null;
        return l[Random.Range(0, l.Count)];

        // This is expensive
        /*
        var l = interactables[t];
        l.Sort((a, b) =>
            (a.transform.position - position).magnitude.CompareTo(
            (b.transform.position - position).magnitude));

        if (l.Count == 0)
            return null;

        while (true)
            for (int i = 0; i < l.Count; ++i)
            {
                if (Random.Range(0, 3) == 0)
                    return l[i];
            }
        */
    }

    public static settler_interactable random(job_type j)
    {
        var l = interactables[j.default_priority];
        if (l.Count == 0) return null;
        return l[Random.Range(0, l.Count)];
    }

    public static void initialize()
    {
        // Initialize the dictionary of interactables
        interactables = new Dictionary<int, List<settler_interactable>>();
        foreach (var j in job_type.all)
            interactables[j.default_priority] = new List<settler_interactable>();
    }

    static void register_interactable(settler_interactable i)
    {
        if (interactables[i.job.default_priority].Contains(i))
            throw new System.Exception("Tried to multiply-register interactable!");
        interactables[i.job.default_priority].Add(i);
    }

    static void forget_interactable(settler_interactable i)
    {
        if (!interactables[i.job.default_priority].Remove(i))
            throw new System.Exception("Tried to remove unregistered interactable!");
    }

    public static string info()
    {
        string ret = "";
        int total = 0;
        foreach (var j in job_type.all)
        {
            int count = interactables[j.default_priority].Count;
            ret += "    " + j.display_name + " : " + count + "\n";
            total += count;
        }
        ret = "Total interactions : " + total + "\n" + ret;
        return ret.TrimEnd();
    }
}
