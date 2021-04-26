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

public abstract class settler_interactable : has_path_elements,
    INonBlueprintable, INonEquipable, IAddsToInspectionText, IExtendsNetworked
{
    /// <summary> The type of interaction this is. This will determine when 
    /// a settler decides to use this interactable object. </summary>
    public skill skill;

    // Overrideable stuff
    protected virtual bool ready_to_assign(settler s) { return true; }
    protected virtual void on_assign(settler s) { }
    protected virtual STAGE_RESULT on_interact(settler s, int stage) { return STAGE_RESULT.TASK_COMPLETE; }
    protected virtual void on_unassign(settler s) { }

    public virtual string task_info() { return name; }
    public virtual float move_to_speed(settler s) { return s.walk_speed; }

    public void interact(settler s)
    {
        cancel_missing_worker_timeout();
        switch (on_interact(s, stage.value))
        {
            case STAGE_RESULT.TASK_COMPLETE:
            case STAGE_RESULT.TASK_FAILED:
                unassign();
                return;

            case STAGE_RESULT.STAGE_COMPLETE:
                ++stage.value;
                return;

            case STAGE_RESULT.STAGE_UNDERWAY:
                return;

            default: throw new System.Exception("Unkown stage result!");
        }
    }

    bool try_assign(settler s)
    {
        if (s == null) return false; // Settler has been deleted
        if (this == null) return false; // I have been deleted

        if (settler_id.value > 0 && settler_id.value != s.network_id)
        {
            if (settler != null)
            {
                cancel_missing_worker_timeout();
                return false; // Already assigned to someone else
            }
            else
            {
                // Already taken, but by someone who is not loaded
                // on this client. However, They might be loaded on
                // someone else's client. But, if they don't show up
                // for work soon, they might not be loaded on
                // any clients; unassign them.
                if (!check_missing_worker_timeout())
                {
                    trigger_missing_worker_timeout();
                    return false;
                }
            }
        }

        if (assigned_to(s) != null)
            return false; // S is already assigned to something else

        if (!skill.possible_when_under_attack && town_gate.group_under_attack(s.group))
            return false; // Not possible when under attack

        // This should be called last so that we can assume within
        // ready_to_assign that the above conditions are met.
        if (!ready_to_assign(s))
            return false; // Not ready to assign anything

        // Assign the new settler
        settler_id.value = s.network_id;
        stage.value = 0;
        on_assign(s);

        return true;
    }

    public void unassign()
    {
        // Unassign the settler
        on_unassign(settler);
        settler_id.value = -1;
        stage.value = 0;
    }

    //########################//
    // Missing worker timeout //
    //########################//

    const float MISSING_WORKER_TIMEOUT = 5f;

    // Timer for automatic unassign if we loose track of asignee
    float force_unassign_time = float.MaxValue;

    void trigger_missing_worker_timeout()
    {
        if (force_unassign_time > Time.realtimeSinceStartup + MISSING_WORKER_TIMEOUT)
            force_unassign_time = Time.realtimeSinceStartup + MISSING_WORKER_TIMEOUT;
    }

    void cancel_missing_worker_timeout()
    {
        force_unassign_time = float.MaxValue;
    }

    bool check_missing_worker_timeout()
    {
        if (force_unassign_time < Time.realtimeSinceStartup)
        {
            cancel_missing_worker_timeout();
            unassign();
            return true;
        }
        return false;
    }

    //#################//
    // UNITY callbacks //
    //#################//

    protected virtual void Start()
    {
        register_interactable(this);
    }

    protected virtual void OnDestroy()
    {
        forget_interactable(this);
    }

    protected virtual void OnDrawGizmos()
    {
        if (settler == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, settler.transform.position + Vector3.up);
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_int settler_id;
    networked_variables.net_int stage;

    public settler settler
    {
        get
        {
            if (settler_id.value <= 0) return null;
            var nw = networked.try_find_by_id(settler_id.value, false);
            if (nw == null) return null; // Settler is not loaded on this client
            if (nw is settler) return (settler)nw;

            // The networked id has been taken by something else?
            Debug.LogError("Settler network id overwritten?");
            return null;
        }
    }

    public virtual IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                // Initialize the settler id/stage
                settler_id = new networked_variables.net_int(default_value: -1);
                stage = new networked_variables.net_int();
            },

            on_auth_change = (has_auth) =>
            {
                // Reset assignment on authority change
                settler_id.value = -1;
                stage.value = 0;
            }
        };
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public virtual string added_inspection_text()
    {
        check_missing_worker_timeout();
        if (settler != null) return settler.net_name.value.capitalize() + " is assigned to this.";
        else if (settler_id.value > 0)
        {
            float timeout_in = force_unassign_time - Time.realtimeSinceStartup;
            if (timeout_in < MISSING_WORKER_TIMEOUT * 2)
                return "Reserved by someone (timeout in " + timeout_in.ToString("F1") + ")";
            return "Reserved by someone";
        }
        if (skill == null) return "Required skill type undetermined.";
        if (!skill.is_visible) return null;
        return "Requires the " + skill.display_name + " skill.";
    }

    //##############//
    // STATIC STUFF //
    //##############//

    // Private static //

    static HashSet<settler_interactable> interactables = new HashSet<settler_interactable>();

    static void register_interactable(settler_interactable i)
    {
        interactables.Add(i);
    }

    static void forget_interactable(settler_interactable i)
    {
        interactables.Remove(i);
    }

    // Public static //

    public static bool try_assign_interaction(settler s)
    {
        foreach (var sk in skill.all)
        {
            // Skip visible skills that fail the priority test
            if (sk.is_visible && !skill.priority_test(s.job_priorities[sk])) continue;

            var possibilities = new List<settler_interactable>();
            foreach (var i in interactables)
                if (i.skill == sk)
                    possibilities.Add(i);

            if (possibilities.Count == 0)
                continue;

            for (int i = 0; i < load_balancing.iter; ++i)
            {
                if (possibilities[Random.Range(0, possibilities.Count)].try_assign(s))
                    return true;
            }
        }

        return false;
    }

    public static settler_interactable assigned_to(settler s)
    {
        foreach (var i in interactables)
            if (i.settler_id.value == s.network_id)
                return i;
        return null;
    }

    public static string info()
    {
        return "Total interactions : " + interactables.Count;
    }

    protected enum STAGE_RESULT
    {
        STAGE_COMPLETE,
        TASK_COMPLETE,
        TASK_FAILED,
        STAGE_UNDERWAY,
    }
}

public abstract class walk_to_settler_interactable : settler_interactable
{
    town_path_element.path path;
    protected bool arrived { get; private set; }

    protected virtual void on_arrive(settler s) { }
    protected virtual STAGE_RESULT on_interact_arrived(settler s, int stage) { return STAGE_RESULT.TASK_COMPLETE; }

    protected sealed override void on_assign(settler s)
    {
        arrived = false;
        path = null;
    }

    protected sealed override STAGE_RESULT on_interact(settler s, int stage)
    {
        // Delegate control to implementation once I have arrived
        if (stage > 0) return on_interact_arrived(s, stage - 1);

        // Only authority clients control the settler
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Get a path
        if (path == null)
        {
            path = new town_path_element.path(s.path_element, path_element(s.group));
            if (!path.valid) return STAGE_RESULT.TASK_FAILED; // Pathfinding failed
        }

        // Path walking complete
        if (path.Count == 0)
        {
            arrived = true;
            on_arrive(s);
            return STAGE_RESULT.STAGE_COMPLETE;
        }

        var next_element = path.walk(s.transform, move_to_speed(s), s);
        if (next_element != null) s.path_element = next_element;

        return STAGE_RESULT.STAGE_UNDERWAY;
    }
}