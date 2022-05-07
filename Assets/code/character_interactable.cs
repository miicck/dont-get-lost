using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class has_path_elements : MonoBehaviour
{
    /// <summary> Returns the path element that a character from the
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

public abstract class character_interactable : has_path_elements,
    INonBlueprintable, INonEquipable, IAddsToInspectionText, IExtendsNetworked
{
    /// <summary> The type of interaction this is. This will determine when 
    /// a character decides to use this interactable object. </summary>
    public skill skill;

    // Overrideable stuff
    protected virtual bool ready_to_assign(character c) => true;
    protected virtual void on_assign(character c) { }
    protected virtual STAGE_RESULT on_interact(character c, int stage) => STAGE_RESULT.TASK_COMPLETE;
    protected virtual void on_unassign(character c) { }

    public abstract string task_summary();

    float delta_xp = 0;

    public void interact(character c)
    {
        if (!possible_checks(c))
        {
            unassign();
            return;
        }

        if (current_proficiency == null)
        {
            current_proficiency = new proficiency_info(c, this);

            if (c is settler)
            {
                var s = c as settler;
                if (s.skills[skill].level > 20)
                    s.add_mood_effect("excellent_at");
                else if (s.skills[skill].level > 10)
                    s.add_mood_effect("great_at");
                else if (s.skills[skill].level > 5)
                    s.add_mood_effect("good_at");
            }
        }

        if (c.has_authority)
        {
            // Gain XP
            delta_xp += Time.deltaTime;
            if (delta_xp > 1f)
            {
                delta_xp = 0;
                if (c is settler)
                    (c as settler).skills.modify_xp(skill, skill.XP_GAIN_PER_SEC);
            }
        }

        cancel_missing_worker_timeout();
        switch (on_interact(c, stage.value))
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

    /// <summary> By default, only settlers can carry out interactions. 
    /// Override this method to change this behaviour. </summary>
    protected virtual bool compatible_character(character c) => c is settler;

    bool possible_checks(character c)
    {
        // This gets called every frame to check that this
        // interaction is still possible, so should not be too slow.

        if (this == null) return false; // This has been deleted
        if (c == null) return false; // character has been deleted

        if (c is settler)
        {
            var s = (settler)c;

            if (skill.family != skill.SKILL_FAMILY.DEFENSIVE && group_info.under_attack(s.group) && s.can_defend)
                return false; // Defenders can't do non-defensive tasks when town is under attack

            if (s.starving && skill.family != skill.SKILL_FAMILY.EATING && group_has_food_available_for(s))
                return false; // Not possible when starving/there is food available (unless this is an eating task)
        }

        return true;
    }

    protected enum ASSIGN_FAILURE_MODE
    {
        NO_FAILURE,
        CHARACTER_IS_NULL,
        INTERACTABLE_IS_NULL,
        WRONG_GROUP,
        ALREADY_ASSIGNED,
        ALREADY_ASSIGNED_TO_UNLOADED,
        NOT_POSSIBLE,
        NOT_READY,
        INCOMPATIBLE_CHARACTER,
    }

    bool can_assign(character c, out ASSIGN_FAILURE_MODE failure)
    {
        if (c == null)
        {
            failure = ASSIGN_FAILURE_MODE.CHARACTER_IS_NULL;
            return false; // character has been deleted
        }

        if (this == null)
        {
            failure = ASSIGN_FAILURE_MODE.INTERACTABLE_IS_NULL;
            return false; // I have been deleted
        }

        if (!compatible_character(c))
        {
            failure = ASSIGN_FAILURE_MODE.INCOMPATIBLE_CHARACTER;
            return false; // This character is incompatible with this interaction
        }

        if (path_element_count > 0 && path_element(c.group) == null)
        {
            failure = ASSIGN_FAILURE_MODE.WRONG_GROUP;
            return false; // This interactable does not have the same group as me
        }

        if (character_id.value > 0 && character_id.value != c.network_id)
        {
            if (character != null)
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
        if (!possible_checks(c))
        {
            failure = ASSIGN_FAILURE_MODE.NOT_POSSIBLE;
            return false;
        }

        // This should be called last so that we can assume within
        // ready_to_assign that the above conditions are met.
        if (!ready_to_assign(c))
        {
            failure = ASSIGN_FAILURE_MODE.NOT_READY;
            return false;
        }

        failure = ASSIGN_FAILURE_MODE.NO_FAILURE;
        return true;
    }

    bool try_assign(character c, out ASSIGN_FAILURE_MODE failure)
    {
        if (!can_assign(c, out failure))
        {
            if (failure == ASSIGN_FAILURE_MODE.NO_FAILURE)
                Debug.LogError("Assign failed but no reason set!");
            return false;
        }

        // Assign the new character
        character_id.value = c.network_id;
        current_proficiency = null;
        failure = ASSIGN_FAILURE_MODE.NO_FAILURE;
        return true;
    }

    protected virtual void on_fail_assign(character c, ASSIGN_FAILURE_MODE failure) { }

    public void unassign()
    {
        // Unassign the character
        character_id.value = -1;
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

        public proficiency_info(character s, character_interactable i)
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

    protected virtual List<proficiency> proficiencies(character c)
    {
        if (c is settler)
        {
            var s = (settler)c;
            var ret = new List<proficiency>();
            ret.Add(new proficiency(s.skills[skill].proficiency_modifier, "skill level"));
            int total_mood = s.total_mood();
            if (total_mood != 0) ret.Add(new proficiency(total_mood * 5, "mood"));
            return ret;
        }

        return new List<proficiency>() { };
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

    protected virtual void Start() => register_interactable(this);
    protected virtual void OnDestroy() => forget_interactable(this);

    protected virtual void OnDrawGizmos()
    {
        if (character == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, character.transform.position + Vector3.up);
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_int character_id;
    networked_variables.net_int stage;

    public character character
    {
        get
        {
            if (character_id == null || character_id.value <= 0) return null;
            var nw = networked.try_find_by_id(character_id.value, false);
            if (nw == null) return null; // character is not loaded on this client
            if (nw is character)
            {
                var c = (character)nw;
                if (c.is_dead)
                {
                    character_id.value = -1;
                    return null;
                }
                return c;
            }

            // The networked id has been taken by something else?
            Debug.LogError("Interactable ID set to a non-character");
            character_id.value = -1;
            return null;
        }
    }

    public virtual IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                // Initialize the character id/stage
                character_id = new networked_variables.net_int(default_value: -1);
                stage = new networked_variables.net_int();

                character_id.on_change_old_new = (old_val, new_val) =>
                {
                    stage.value = 0;
                    delta_xp = 0;

                    assignments.Remove(old_val);
                    if (this == null) return;

                    if (new_val >= 0) assignments[new_val] = this;

                    var old_user = networked.try_find_by_id(old_val, false);
                    if (old_user is character) on_unassign(old_user as character);

                    var new_user = networked.try_find_by_id(new_val, false);
                    if (new_user is character)
                    {
                        // Add mood effects from uncovered work spots
                        if (new_user is settler)
                            foreach (var me in GetComponentsInChildren<uncovered_mood_effect>())
                                if (!weather.spot_is_covered(me.transform.position))
                                    (new_user as settler).add_mood_effect(me.effect.name);

                        on_assign(new_user as character);
                    }
                };

                stage.on_change_old_new = on_stage_change;
            }
        };
    }

    protected virtual void on_stage_change(int old_stage, int new_stage) { }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public virtual string added_inspection_text()
    {
        check_missing_worker_timeout();

        string info = null;

        if (character != null)
        {
            string name = character.display_name;
            if (character is settler) name = (character as settler).net_name.value;
            info = name.capitalize() + " is assigned to this";
        }
        else if (character_id.value > 0)
        {
            float timeout_in = force_unassign_time - Time.realtimeSinceStartup;
            if (timeout_in < MISSING_WORKER_TIMEOUT * 2)
                info = "Reserved by someone (timeout in " + timeout_in.ToString("F1") + ")";
            else
                info = "Reserved by someone";
        }

        if (skill == null) info += "\nRequired skill type undetermined";
        if (skill.is_visible)
            info += "\nRequires the " + skill.display_name + " skill";

        return info;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    // Private static //

    static Dictionary<skill, List<character_interactable>> interactables =
        new Dictionary<skill, List<character_interactable>>();

    static Dictionary<int, character_interactable> assignments =
        new Dictionary<int, character_interactable>();

    static void register_interactable(character_interactable i)
    {
        if (interactables.TryGetValue(i.skill, out List<character_interactable> hs)) hs.Add(i);
        else interactables[i.skill] = new List<character_interactable> { i };
    }

    static void forget_interactable(character_interactable i)
    {
        if (interactables.TryGetValue(i.skill, out List<character_interactable> hs)) hs.Remove(i);
    }

    static character_interactable try_assign_interaction(character c)
    {
        // Can't assign interactions on non-auth clients
        if (!c.has_authority) return null;

        var to_search = c is settler ? (c as settler).skill_priority_order : skill.all;

        foreach (var sk in to_search)
        {
            if (sk.is_visible && c is settler)
                if (Random.Range(0, 1f) < sk.skip_probability(c as settler))
                    continue;

            if (!interactables.TryGetValue(sk, out List<character_interactable> possibilities)) continue;
            if (possibilities.Count == 0) continue;

            for (int i = 0; i < load_balancing.iter; ++i)
            {
                var to_try = possibilities[Random.Range(0, possibilities.Count)];
                if (to_try.try_assign(c, out ASSIGN_FAILURE_MODE fm)) return to_try;
                else to_try.on_fail_assign(c, fm);
            }
        }

        return null;
    }

    public static HashSet<character_interactable> available_eating_interactions(settler s)
    {
        HashSet<character_interactable> ret = new HashSet<character_interactable>();
        foreach (var i in interactables[Resources.Load<skill>("skills/eating")])
            if (i.can_assign(s, out ASSIGN_FAILURE_MODE fail_mode))
                ret.Add(i);
        return ret;
    }

    // Public static //

    public static character_interactable assigned_to(character c)
    {
        // Look for existing interaction
        if (assignments.TryGetValue(c.network_id, out character_interactable i))
            return i;

        // As a fallback, search all interactions for one assigend to s
        foreach (var kv in interactables)
            foreach (var inter in kv.Value)
                if (inter.character_id.value == c.network_id)
                {
                    Debug.Log("Assignement fallback triggered!");
                    inter.current_proficiency = null;
                    assignments[c.network_id] = inter;
                    return inter;
                }

        // None found, attempt to assign interaction
        // if we're on the authority client
        if (c.has_authority)
            try_assign_interaction(c);

        return null;
    }

    public static bool force_assign(character_interactable i, character c)
    {
        if (c == null) return false;

        assigned_to(c)?.unassign(); // Unassign previous task of s
        i?.unassign(); // Unassign previous character from i

        if (i != null && !i.try_assign(c, out ASSIGN_FAILURE_MODE fm)) // Assign i to s
        {
            Debug.LogError("Force assign failed: " + fm);
            return false;
        }

        return true;
    }

    public static bool group_has_food_available_for(settler c)
    {
        if (!interactables.TryGetValue(Resources.Load<skill>("skills/eating"),
            out List<character_interactable> eating_interactions))
            return false; // No eating interactions available

        // Check for eating interactables that are assignable to s
        foreach (var e in eating_interactions)
            if (e.can_assign(c, out ASSIGN_FAILURE_MODE fm))
                return true;

        return false;
    }

    public static string info() => "Total interactions : " + interactables.Count;

    protected enum STAGE_RESULT
    {
        STAGE_COMPLETE,
        TASK_COMPLETE,
        TASK_FAILED,
        STAGE_UNDERWAY,
    }
}

public abstract class character_walk_to_interactable : character_interactable
{
    town_path_element.path path;
    protected bool arrived { get; private set; }

    protected virtual void on_arrive(character c) { }
    protected virtual STAGE_RESULT on_interact_arrived(character c, int stage) => STAGE_RESULT.TASK_COMPLETE;
    public virtual float move_to_speed(character c) => c.walk_speed;

    protected sealed override void on_assign(character c)
    {
        arrived = false;
        path = null;
    }

    void lerp_look(character s, float amt)
    {
        // Lerp character so they are facing towards this
        Vector3 delta = transform.position - s.transform.position;
        delta.y = 0;
        s.transform.forward = Vector3.Lerp(s.transform.forward, delta, amt);
    }

    protected sealed override STAGE_RESULT on_interact(character c, int stage)
    {
        // Delegate control to implementation once I have arrived
        if (stage > 0)
        {
            if (!arrived)
            {
                arrived = true;
                lerp_look(c, 1f); // Look at target
                on_arrive(c);

                // Check if this spot is covered
            }

            return on_interact_arrived(c, stage - 1);
        }

        // Only authority clients control the character
        if (!c.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Get a path
        if (path == null)
        {
            path = town_path_element.path.get(c.town_path_element, path_element(c.group));
            if (path == null) return STAGE_RESULT.TASK_FAILED; // Pathfinding failed
        }

        switch (path.walk(c, move_to_speed(c)))
        {
            case town_path_element.path.WALK_STATE.COMPLETE:
                return STAGE_RESULT.STAGE_COMPLETE;

            case town_path_element.path.WALK_STATE.UNDERWAY:
                if (path.index == path.count - 1)
                {
                    // Turn towards the target on the last path segment
                    lerp_look(c, Time.deltaTime * 5f);
                }
                return STAGE_RESULT.STAGE_UNDERWAY;

            default:
                return STAGE_RESULT.TASK_FAILED;
        }
    }
}