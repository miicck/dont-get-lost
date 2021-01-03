using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_interactable : MonoBehaviour, INonBlueprintable, INonEquipable
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

    /// <summary> Returns the networked object to which I belong. </summary>
    public networked networked_parent => GetComponentInParent<networked>();

    /// <summary> Returns the assignments of settlers to this interactable, if they exist. </summary>
    public settler_task_assignment[] assignments => networked_parent?.GetComponentsInChildren<settler_task_assignment>();

    /// <summary> The type of interaction this is. This will determine when 
    /// a settler decides to use this interactable object. </summary>
    public TYPE type;

    /// <summary> The maximum number of settlers that 
    /// can be assigned to this interactable. </summary>
    public int max_simultaneous_users = 1;

    public enum TYPE
    {
        WORK,
        EAT,
        SLEEP,
        GUARD,
    }

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

    public virtual void on_assign(settler s) { }
    public virtual void on_unassign(settler s) { }
    public virtual void on_interact(settler s) { }
    public virtual bool is_complete(settler s) { return true; }
    public virtual string task_info() { return name; }
    public virtual float move_to_speed(settler s) { return s.walk_speed; }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<TYPE, List<settler_interactable>> interactables;

    public static settler_interactable nearest(TYPE t, Vector3 v)
    {
        return utils.find_to_min(interactables[t],
            (i) => (i.transform.position - v).sqrMagnitude);
    }

    public delegate bool accept_func(settler_interactable i);
    public static settler_interactable nearest_acceptable(TYPE t, Vector3 v, accept_func f)
    {
        var ret = utils.find_to_min(interactables[t], (i) =>
        {
            if (!f(i)) return Mathf.Infinity; // i not acceptable => infinite score
            return (i.transform.position - v).magnitude; // distance = score
        });

        // Only return if acceptable
        if (f(ret)) return ret;
        return null;
    }

    public static settler_interactable proximity_weighted_ramdon(TYPE t, Vector3 position)
    {
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
    }

    public static settler_interactable random(TYPE t)
    {
        var l = interactables[t];
        if (l.Count == 0) return null;
        return l[Random.Range(0, l.Count)];
    }

    public static settler_interactable random()
    {
        var types = System.Enum.GetValues(typeof(TYPE));
        return random((TYPE)types.GetValue(Random.Range(0, types.Length)));
    }

    public static void initialize()
    {
        // Initialize the dictionary of interactables
        interactables = new Dictionary<TYPE, List<settler_interactable>>();
        foreach (TYPE e in System.Enum.GetValues(typeof(TYPE)))
            interactables[e] = new List<settler_interactable>();
    }

    static void register_interactable(settler_interactable i)
    {
        if (interactables[i.type].Contains(i))
            throw new System.Exception("Tried to multiply-register interactable!");
        interactables[i.type].Add(i);
    }

    static void forget_interactable(settler_interactable i)
    {
        if (!interactables[i.type].Remove(i))
            throw new System.Exception("Tried to remove unregistered interactable!");
    }

    public static string info()
    {
        string ret = "";
        int total = 0;
        foreach (TYPE e in System.Enum.GetValues(typeof(TYPE)))
        {
            int count = interactables[e].Count;
            ret += "    " + e + " : " + count + "\n";
            total += count;
        }
        ret = "Total interactions : " + total + "\n" + ret;
        return ret.TrimEnd();
    }
}
