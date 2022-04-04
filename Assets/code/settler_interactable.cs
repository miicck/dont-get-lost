using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class has_path_elements : MonoBehaviour
{
    /// <summary> Returns the path element that a settler from the
    /// given group can use to access this interactable. If 
    /// <paramref name="group"/> is < 0, then the first element found
    /// will be returned. </summary>
    public virtual town_path_element path_element(int group = -1)
    {
        if (this == null) return null;
        foreach (var e in GetComponentsInChildren<town_path_element>())
            if (e.group == group || group < 0)
                return e;
        return null;
    }

    public int path_element_count => GetComponentsInChildren<town_path_element>().Length;
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

    public abstract string task_summary();

    float delta_xp = 0;

    public void interact(settler s)
    {
        if (!possible_checks(s))
        {
            unassign();
            return;
        }

        if (current_proficiency == null)
            current_proficiency = new proficiency_info(s, this);

        if (s.has_authority)
        {
            // Gain XP
            delta_xp += Time.deltaTime;
            if (delta_xp > 1f)
            {
                delta_xp = 0;
                settler.skills.modify_xp(skill, skill.XP_GAIN_PER_SEC);
            }
        }

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

    bool possible_checks(settler s)
    {
        if (this == null) return false; // This has been deleted
        if (s == null) return false; // Settler has been deleted

        if (!skill.possible_when_under_attack && group_info.under_attack(s.group))
            return false; // Not possible when under attack

        if (s.starving && skill.name != "eating" && group_has_food_available_for(s))
            return false; // Not possible when starving/there is food available (unless this is an eating task)

        return true;
    }

    protected enum ASSIGN_FAILURE_MODE
    {
        NO_FAILURE,
        SETTLER_IS_NULL,
        INTERACTABLE_IS_NULL,
        WRONG_GROUP,
        ALREADY_ASSIGNED,
        ALREADY_ASSIGNED_TO_UNLOADED,
        NOT_POSSIBLE,
        NOT_READY,
    }

    bool can_assign(settler s, out ASSIGN_FAILURE_MODE failure)
    {
        if (s == null)
        {
            failure = ASSIGN_FAILURE_MODE.SETTLER_IS_NULL;
            return false; // Settler has been deleted
        }

        if (this == null)
        {
            failure = ASSIGN_FAILURE_MODE.INTERACTABLE_IS_NULL;
            return false; // I have been deleted
        }

        if (path_element_count > 0 && path_element(s.group) == null)
        {
            failure = ASSIGN_FAILURE_MODE.WRONG_GROUP;
            return false; // This interactable does not have the same group as me
        }

        if (settler_id.value > 0 && settler_id.value != s.network_id)
        {
            if (settler != null)
            {
                cancel_missing_worker_timeout();
                failure = ASSIGN_FAILURE_MODE.ALREADY_ASSIGNED;
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
                    failure = ASSIGN_FAILURE_MODE.ALREADY_ASSIGNED_TO_UNLOADED;
                    return false;
                }
            }
        }

        // Check if this is possible
        if (!possible_checks(s))
        {
            failure = ASSIGN_FAILURE_MODE.NOT_POSSIBLE;
            return false;
        }

        // This should be called last so that we can assume within
        // ready_to_assign that the above conditions are met.
        if (!ready_to_assign(s))
        {
            failure = ASSIGN_FAILURE_MODE.NOT_READY;
            return false;
        }

        failure = ASSIGN_FAILURE_MODE.NO_FAILURE;
        return true;
    }

    bool try_assign(settler s, out ASSIGN_FAILURE_MODE failure)
    {
        if (!can_assign(s, out failure)) return false;

        // Assign the new settler
        settler_id.value = s.network_id;
        current_proficiency = null;
        failure = ASSIGN_FAILURE_MODE.NO_FAILURE;
        return true;
    }

    protected virtual void on_fail_assign(settler s, ASSIGN_FAILURE_MODE failure) { }

    public void unassign()
    {
        // Unassign the settler
        settler_id.value = -1;
        current_proficiency?.on_unassign();
        current_proficiency = null;
    }

    //#############//
    // Proficiency //
    //#############//

    public proficiency_info current_proficiency { get; private set; }

    public class proficiency_info
    {
        public float total_multiplier { get; private set; }
        public int total_proficiency { get; private set; }

        List<proficiency> proficiencies;
        public void on_unassign() { foreach (var p in proficiencies) p.on_unassign(); }

        public proficiency_info(settler s, settler_interactable i)
        {
            // Get the proficiencies (sort by highest effect)
            proficiencies = i.proficiencies(s);
            proficiencies.Sort((a, b) => Mathf.Abs(a.percent_modifier) < Mathf.Abs(b.percent_modifier) ? 1 : -1);

            // Work out the total multiplier
            total_multiplier = 1f;
            foreach (var p in proficiencies)
                total_multiplier *= 1f + p.percent_modifier * 0.01f;

            // Work out the equivelant as a persentage
            total_proficiency = (int)((total_multiplier - 1f) * 100f);
        }

        public string summary()
        {
            return "Work speed: " + percent_to_string(total_proficiency);
        }

        public string breakdown()
        {
            string ret = summary();
            foreach (var p in proficiencies)
                ret += "\n  " + p.description + " " + percent_to_string(p.percent_modifier, clamp_check: true);
            return ret;
        }

        static string percent_to_string(int modifier, bool clamp_check = false)
        {
            string ret = (modifier >= 0 ? "+" : "") + modifier + "%";
            if (clamp_check)
            {
                if (modifier == proficiency.MIN_MODIFIER) ret += " (minimum possible)";
                if (modifier == proficiency.MAX_MODIFIER) ret += " (maximum possible)";
            }
            return ret;
        }
    }

    protected class proficiency
    {
        public const int MIN_MODIFIER = -75;
        public const int MAX_MODIFIER = 200;

        public int percent_modifier
        {
            get => _percent_modifier;

            private set
            {
                if (value < MIN_MODIFIER) value = MIN_MODIFIER;
                if (value > MAX_MODIFIER) value = MAX_MODIFIER;
                _percent_modifier = value;
            }
        }
        int _percent_modifier;

        public string description { get; private set; }

        public proficiency(int percent_modifier, string description)
        {
            this.percent_modifier = percent_modifier;
            this.description = description;
        }

        public virtual void on_unassign() { }
    }

    protected class item_based_proficiency : proficiency
    {
        item item;
        inventory container;
        float break_prob;

        public item_based_proficiency(int modifier, string description,
            inventory container, item item, float break_prob) : base(modifier, description)
        {
            this.container = container;
            this.item = item;
            this.break_prob = break_prob;
        }

        public override void on_unassign()
        {
            if (container == null) return;
            if (item == null) return;
            if (Random.Range(0, 1f) < break_prob)
                container.remove(item, 1);
        }
    }

    protected virtual List<proficiency> proficiencies(settler s)
    {
        var ret = new List<proficiency>();
        ret.Add(new proficiency(s.skills[skill].proficiency_modifier, "skill level"));
        int total_mood = s.total_mood();
        if (total_mood != 0) ret.Add(new proficiency(total_mood * 5, "mood"));
        return ret;
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
            if (settler_id == null || settler_id.value <= 0) return null;
            var nw = networked.try_find_by_id(settler_id.value, false);
            if (nw == null) return null; // Settler is not loaded on this client
            if (nw is settler) return (settler)nw;

            // The networked id has been taken by something else?
            Debug.Log("Interactable ID set to a non-setter");
            settler_id.value = -1;
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

                settler_id.on_change_old_new = (old_val, new_val) =>
                {
                    stage.value = 0;
                    delta_xp = 0;

                    assignments.Remove(old_val);
                    if (this == null) return;

                    if (new_val >= 0) assignments[new_val] = this;

                    var old_user = networked.try_find_by_id(old_val, false);
                    if (old_user is settler) on_unassign((settler)old_user);

                    var new_user = networked.try_find_by_id(new_val, false);
                    if (new_user is settler) on_assign((settler)new_user);
                };
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

    static Dictionary<skill, List<settler_interactable>> interactables =
        new Dictionary<skill, List<settler_interactable>>();

    static Dictionary<int, settler_interactable> assignments =
        new Dictionary<int, settler_interactable>();

    static void register_interactable(settler_interactable i)
    {
        if (interactables.TryGetValue(i.skill, out List<settler_interactable> hs)) hs.Add(i);
        else interactables[i.skill] = new List<settler_interactable> { i };
    }

    static void forget_interactable(settler_interactable i)
    {
        if (interactables.TryGetValue(i.skill, out List<settler_interactable> hs)) hs.Remove(i);
    }

    static settler_interactable try_assign_interaction(settler s)
    {
        // Can't assign interactions on non-auth clients
        if (!s.has_authority) return null;

        foreach (var sk in skill.all)
        {
            // Skip visible skills that fail the priority test
            if (sk.is_visible && !sk.priority_test(s)) continue;
            if (!interactables.TryGetValue(sk, out List<settler_interactable> possibilities)) continue;
            if (possibilities.Count == 0) continue;

            for (int i = 0; i < load_balancing.iter; ++i)
            {
                var to_try = possibilities[Random.Range(0, possibilities.Count)];
                if (to_try.try_assign(s, out ASSIGN_FAILURE_MODE fm)) return to_try;
                else to_try.on_fail_assign(s, fm);
            }
        }

        return null;
    }

    // Public static //

    public static settler_interactable assigned_to(settler s)
    {
        // Look for existing interaction
        if (assignments.TryGetValue(s.network_id, out settler_interactable i))
            return i;

        // As a fallback, search all interactions for one assigend to s
        foreach (var kv in interactables)
            foreach (var inter in kv.Value)
                if (inter.settler_id.value == s.network_id)
                {
                    Debug.Log("Assignement fallback triggered!");
                    inter.current_proficiency = null;
                    assignments[s.network_id] = inter;
                    return inter;
                }

        // None found, attempt to assign interaction
        // if we're on the authority client
        if (s.has_authority)
            try_assign_interaction(s);

        return null;
    }

    public static bool force_assign(settler_interactable i, settler s)
    {
        if (s == null) return false;

        assigned_to(s)?.unassign(); // Unassign previous task of s
        i?.unassign(); // Unassign previous settler from i

        if (i != null && !i.try_assign(s, out ASSIGN_FAILURE_MODE fm)) // Assign i to s
        {
            Debug.LogError("Force assign failed: " + fm);
            return false;
        }

        return true;
    }

    public static bool group_has_food_available_for(settler s)
    {
        if (!interactables.TryGetValue(Resources.Load<skill>("skills/eating"),
            out List<settler_interactable> eating_interactions))
            return false; // No eating interactions available

        // Check for eating interactables that are assignable to s
        foreach (var e in eating_interactions)
            if (e.can_assign(s, out ASSIGN_FAILURE_MODE fm))
                return true;

        return false;
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
    public virtual float move_to_speed(settler s) { return s.walk_speed; }

    protected sealed override void on_assign(settler s)
    {
        arrived = false;
        path = null;
    }

    void lerp_look(settler s, float amt)
    {
        // Lerp settler so they are facing towards this
        Vector3 delta = transform.position - s.transform.position;
        delta.y = 0;
        s.transform.forward = Vector3.Lerp(s.transform.forward, delta, amt);
    }

    protected sealed override STAGE_RESULT on_interact(settler s, int stage)
    {
        // Delegate control to implementation once I have arrived
        if (stage > 0)
        {
            if (!arrived)
            {
                arrived = true;
                lerp_look(s, 1f); // Look at target
                on_arrive(s);
            }

            return on_interact_arrived(s, stage - 1);
        }

        // Only authority clients control the settler
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Get a path
        if (path == null)
        {
            path = town_path_element.path.get(s.town_path_element, path_element(s.group));
            if (path == null) return STAGE_RESULT.TASK_FAILED; // Pathfinding failed
        }

        switch (path.walk(s, move_to_speed(s)))
        {
            case town_path_element.path.WALK_STATE.COMPLETE:
                return STAGE_RESULT.STAGE_COMPLETE;

            case town_path_element.path.WALK_STATE.UNDERWAY:
                if (path.index == path.count - 1)
                {
                    // Turn towards the target on the last path segment
                    lerp_look(s, Time.deltaTime * 5f);
                }
                return STAGE_RESULT.STAGE_UNDERWAY;

            default:
                return STAGE_RESULT.TASK_FAILED;
        }
    }
}