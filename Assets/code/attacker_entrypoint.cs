using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class attacker_entrypoint : MonoBehaviour, INonEquipable, INonBlueprintable, IExtendsNetworked
{
    public const float MIN_EXTERNAL_PATH_DISTANCE = 15f;
    public const float MAX_EXTERNAL_PATH_DISTANCE = 25f;

    const float MIN_TIME_BETWEEN_PATHS = 1f;
    const float MAX_TIME_BETWEEN_PATHS = 5f;

    path path;
    float next_path_time = float.NegativeInfinity;
    float time_between_paths = MIN_TIME_BETWEEN_PATHS;

    public bool path_complete => path is explicit_path;
    public Vector3 path_end => path == null ? transform.position : path[path.length - 1];
    public town_path_element element => GetComponentInParent<town_path_element>();
    public bool has_authority => GetComponentInParent<networked>()?.has_authority == true;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start() => entrypoints.Add(this);
    private void OnDestroy() => entrypoints.Remove(this);

    private void Update()
    {
        // Don't do anything until the world has loaded
        if (game.loading) return;

        if (!element.is_extremety)
        {
            // Only path from extremities
            draw_path = false;
            path = null;
            return;
        }

        // Draw the path if path links are to be drawn
        draw_path = town_path_element.draw_links;

        if (path == null)
        {
            // Wait until next path time
            if (Time.time < next_path_time) return;

            path = new random_path(transform.position, new agent(), endpoint_success,
                midpoint_successful: midpoint_success,
                starting_direction: element.out_of_town_direction);
        }

        if (path is explicit_path)
        {
            // We have an optimized path, check it remains valid
            if (!path.validate(load_balancing.iter)) path = null;
            else
            {
                settler.try_spawn(element.group, element.transform.position);
                visiting_character.try_spawn(this);
            }
            return;
        }

        switch (path.state)
        {
            case path.STATE.COMPLETE:

                // Optimize path
                time_between_paths = MIN_TIME_BETWEEN_PATHS;
                if (path.optimize(load_balancing.iter))
                    refresh_drawn_path();

                // Optimization complete
                else
                {
                    // Attempt to cut off path at first point in town
                    for (int i = path.length - 1; i >= 0; --i)
                    {
                        var e = town_path_element.nearest_element(path[i], group: element.group);
                        if (e.linkable_region.Contains(path[i]))
                        {
                            List<Vector3> new_path = new List<Vector3>();
                            for (int j = i; j < path.length; ++j)
                                new_path.Add(path[j]);
                            path = new explicit_path(new_path, new agent());
                            refresh_drawn_path();
                            break;
                        }
                    }

                    // Fallback, keep whole path
                    List<Vector3> whole_path = new List<Vector3>();
                    for (int i = 0; i < path.length; ++i) whole_path.Add(path[i]);
                    path = new explicit_path(whole_path, new agent());
                }

                break;

            case path.STATE.SEARCHING:
                // Run pathfinding
                path.pathfind(load_balancing.iter);
                break;

            case path.STATE.FAILED:
                // Pathfinding failed, try again
                path = null;
                time_between_paths = Mathf.Min(time_between_paths + 0.5f, MAX_TIME_BETWEEN_PATHS);
                next_path_time = Time.time + time_between_paths;
                break;
        }
    }

    private void OnDrawGizmos()
    {
        path?.draw_gizmos();
    }

    //#########//
    // PATHING //
    //#########//

    bool midpoint_success(Vector3 v) => (transform.position - v).magnitude > MAX_EXTERNAL_PATH_DISTANCE;
    bool endpoint_success(Vector3 v) => (transform.position - v).magnitude > MIN_EXTERNAL_PATH_DISTANCE;

    GameObject drawn_path;
    bool draw_path
    {
        get => drawn_path != null;
        set
        {
            if (draw_path == value)
                return; // No change

            if (drawn_path != null) Destroy(drawn_path);
            if (!value || path == null || path.state != path.STATE.COMPLETE) return;

            drawn_path = path.create_visualization(color: Color.red);
            drawn_path?.transform.SetParent(transform);
        }
    }
    void refresh_drawn_path() { draw_path = !draw_path; draw_path = !draw_path; }

    class agent : IPathingAgent
    {
        public float resolution => 1f;
        public float ground_clearance => 0.25f;
        public float height => 1.5f;

        public Vector3 validate_position(Vector3 v, out bool valid)
        {
            v = pathfinding_utils.validate_walking_position(v, resolution, out valid);
            if (v.y < world.SEA_LEVEL) valid = false;
            return v;
        }

        public bool validate_move(Vector3 a, Vector3 b) =>
            pathfinding_utils.validate_walking_move(a, b, resolution, height, ground_clearance, max_angle: 50f);
    }

    //#########//
    // ATTACKS //
    //#########//

    public class approach_controller : ICharacterController
    {
        ICharacterController controller_on_arival;
        attacker_entrypoint entrypoint;
        bool should_run;
        int index = 0;

        public bool complete => index >= entrypoint.path.length;

        public approach_controller(attacker_entrypoint entrypoint,
            ICharacterController controller_on_arival, bool should_run = true)
        {
            this.entrypoint = entrypoint;
            this.controller_on_arival = controller_on_arival;
            this.should_run = should_run;
        }

        public void control(character c)
        {
            if (entrypoint == null ||
                entrypoint.path == null ||
                entrypoint.path.state != path.STATE.COMPLETE)
            {
                // Entrypoint was invalid, die
                c.delete();
                return;
            }

            if (complete)
            {
                // Finished walking the path, begin the attack
                c.controller = controller_on_arival;
                return;
            }

            float speed = should_run ? c.run_speed : c.walk_speed;

            // Make my way into town along the approach path
            if (utils.move_towards_and_look(c.transform, entrypoint.path[
                entrypoint.path.length - 1 - index],
                Time.deltaTime * speed, 0.25f))
                index += 1;
        }

        public void on_end_control(character c) { }
        public void draw_gizmos() { }
        public void draw_inspector_gui() { }
        public string inspect_info() { return "Attacking town."; }
    }

    public class attack_controller : ICharacterController
    {
        town_path_element.path path;
        float local_speed_mod = 1.0f;
        attacker_entrypoint entrypoint;
        settler target;

        public attack_controller(attacker_entrypoint entrypoint)
        {
            this.entrypoint = entrypoint;
            local_speed_mod = Random.Range(0.9f, 1.1f);
        }

        enum STAGE
        {
            SEARCHING_FOR_TARGETS,
            PATHING_TO_TARGET,
            WALKING_TO_TARGET,
            FIGHTING_TARGET,
            PATHING_AWAY,
            GOING_AWAY
        }
        STAGE stage;

        public void control(character c)
        {
            switch (stage)
            {
                case STAGE.SEARCHING_FOR_TARGETS:

                    // Find closest target (with some noise)
                    target = settler.find_to_min((s) =>
                    {
                        if (s.group != entrypoint.element.group) return Mathf.Infinity;
                        return (s.transform.position - c.transform.position).magnitude + Random.Range(0, 5f);
                    });
                    if (target == null) stage = STAGE.PATHING_AWAY;
                    else stage = STAGE.PATHING_TO_TARGET;
                    return;

                case STAGE.PATHING_TO_TARGET:

                    if (target == null)
                    {
                        // No target, go back to searching
                        stage = STAGE.SEARCHING_FOR_TARGETS;
                        break;
                    }

                    path = town_path_element.path.get(c.transform.position,
                        target.transform.position, entrypoint.element.group);

                    if (path == null)
                    {
                        // No path, Go back to searching stage
                        stage = STAGE.SEARCHING_FOR_TARGETS;
                        break;
                    }

                    stage = STAGE.WALKING_TO_TARGET;
                    return;

                case STAGE.WALKING_TO_TARGET:

                    if (path == null || target == null)
                    {
                        // No path or target, go back to searching stage
                        stage = STAGE.SEARCHING_FOR_TARGETS;
                        break;
                    }

                    // Walk the path
                    switch (path.walk(c, c.run_speed * local_speed_mod))
                    {
                        case town_path_element.path.WALK_STATE.UNDERWAY:
                            // Start fight if we're close enough
                            if (c.distance_to(target) < c.pathfinding_resolution)
                            {
                                stage = STAGE.FIGHTING_TARGET;
                                melee_fight.start_fight(c, target);
                            }
                            return;

                        case town_path_element.path.WALK_STATE.FAILED:
                            // Walking failed, go back to searching
                            stage = STAGE.SEARCHING_FOR_TARGETS;
                            break;

                        case town_path_element.path.WALK_STATE.COMPLETE:
                            if (c.distance_to(target) < c.pathfinding_resolution)
                            {
                                // Walking complete, start fighting
                                stage = STAGE.FIGHTING_TARGET;
                                melee_fight.start_fight(c, target);
                            }
                            else
                                // Didn't end up close enough, path again
                                stage = STAGE.PATHING_TO_TARGET;
                            break;
                    }
                    return;

                case STAGE.FIGHTING_TARGET:

                    if (target == null || target.is_dead)
                    {
                        // Fight is over
                        stage = STAGE.SEARCHING_FOR_TARGETS;
                        break;
                    }
                    return;

                case STAGE.PATHING_AWAY:

                    if (entrypoint == null)
                    {
                        // No gate to path to => delete attacker
                        c.delete();
                        break;
                    }

                    // Path to gate
                    path = town_path_element.path.get(
                        town_path_element.nearest_element(c.transform.position, entrypoint.element.group),
                        entrypoint.element);

                    stage = STAGE.GOING_AWAY;
                    return;


                case STAGE.GOING_AWAY:

                    if (path == null)
                    {
                        // No path to walk => delete attacker
                        c.delete();
                        break;
                    }

                    switch (path.walk(c, c.walk_speed))
                    {
                        case town_path_element.path.WALK_STATE.COMPLETE:
                            c.delete(); // Got away
                            break;

                        case town_path_element.path.WALK_STATE.FAILED:
                            stage = STAGE.PATHING_AWAY; // Try pathing again
                            break;

                        case town_path_element.path.WALK_STATE.UNDERWAY:
                            break;
                    }

                    return;
            }
        }

        public void on_end_control(character c) { }

        public string inspect_info()
        {
            switch (stage)
            {
                case STAGE.SEARCHING_FOR_TARGETS: return "Searching for targets";
                case STAGE.PATHING_TO_TARGET: return "Pathfinding (to target)";
                case STAGE.FIGHTING_TARGET: return "Fighting " + target?.name;
                case STAGE.WALKING_TO_TARGET: return "Attacking " + target?.name;
                case STAGE.PATHING_AWAY: return "Pathfinding (leaving)";
                case STAGE.GOING_AWAY: return "Leaving";
                default:
                    Debug.LogError("Unkown attack stage!");
                    return "Attacking";
            }
        }

        public void draw_gizmos()
        {
            path?.draw_gizmos(Color.red);
        }

        public void draw_inspector_gui() { }
    }

    public void trigger_attack(string character)
    {
        var nw = GetComponent<networked>();
        if (nw == null)
        {
            Debug.LogError("Attacker entrypoint is not networked");
            return;
        }
        client.create(path[path.length - 1], "characters/" + character, parent: nw);
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    public IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            on_add_networked_child = (child) =>
            {
                var c = child as character;
                if (c == null) return;
                c.controller = new approach_controller(this, new attack_controller(this));
                attackers.access_or_set(element.group, () => new HashSet<character>()).Add(c);
                update_attack_message();
            },

            on_delete_networked_child = (child) =>
            {
                var c = child as character;
                if (c == null) return;
                if (attackers.TryGetValue(element.group, out HashSet<character> hs))
                {
                    hs.Remove(c);
                    if (hs.Count == 0) attackers.Remove(element.group);
                }
                update_attack_message();
            }
        };
    }

    public void update_attack_message()
    {
        if (attack_message != null)
            Destroy(attack_message);

        Dictionary<string, int> attacker_counts = new Dictionary<string, int>();
        Dictionary<string, character> attacker_examples = new Dictionary<string, character>();
        foreach (var kv in attackers)
            foreach (var attacker in kv.Value)
            {
                if (attacker == null || attacker.is_dead) continue;
                if (attacker_counts.ContainsKey(attacker.display_name))
                    attacker_counts[attacker.display_name] += 1;
                else attacker_counts[attacker.display_name] = 1;
                attacker_examples[attacker.display_name] = attacker;
            }

        if (attacker_counts.Count == 0) return;

        string message = "Under attack!\n(";
        foreach (var kv in attacker_counts)
            message += kv.Value + " " + (kv.Value > 1 ? attacker_examples[kv.Key].plural_name : kv.Key) + " ";
        message = message.Trim();
        message += ")";

        attack_message = gameObject.add_pinned_message(message, Color.red);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static pinned_message attack_message;
    static HashSet<attacker_entrypoint> entrypoints;
    static Dictionary<int, HashSet<character>> attackers;
    static int last_entrypoint_index = 0;

    public static bool attacks_enabled;

    public static List<attacker_entrypoint> valid_entrypoints()
    {
        var ret = new List<attacker_entrypoint>();
        foreach (var e in entrypoints)
            if (e.path_complete)
                ret.Add(e);
        return ret;
    }

    public static void initialize()
    {
        entrypoints = new HashSet<attacker_entrypoint>();
        attackers = new Dictionary<int, HashSet<character>>();
        town_path_element.add_on_groups_update_listener(() =>
        {
            // Re-evaluate attacker groups
            var attackers_new = new Dictionary<int, HashSet<character>>();
            foreach (var kv in attackers)
                foreach (var a in kv.Value)
                {
                    var elm = a.GetComponentInParent<attacker_entrypoint>()?.element;
                    if (elm == null)
                    {
                        Debug.LogError("Attacker had no element!");
                        a.delete();
                        continue;
                    }
                    attackers_new.access_or_set(elm.group, () => new HashSet<character>()).Add(a);
                }
            attackers = attackers_new;
        });

        attacks_enabled = true;
    }

    public static void iterate_over_attackers(int group, group_info.attack_iterator f)
    {
        if (attackers.TryGetValue(group, out HashSet<character> hs))
            foreach (var a in hs)
                if (a != null && f(a))
                    return;
    }

    public static bool group_under_attack(int group)
    {
        if (attackers.TryGetValue(group, out HashSet<character> hs))
            foreach (var a in hs)
                if (!a.is_dead)
                    return true;
        return false;
    }

    public static void trigger_attack(IEnumerable<string> characters_to_spawn)
    {
        // Get all valid entrypoints
        List<attacker_entrypoint> entries = new List<attacker_entrypoint>();
        foreach (var e in entrypoints)
            if (e.path != null && e.path.state == path.STATE.COMPLETE)
                entries.Add(e);
        if (entries.Count == 0) return; // No entrypoints for an attack

        // Attack the group with the most entrypoints
        Dictionary<int, int> group_counts = new Dictionary<int, int>();
        foreach (var e in entrypoints)
        {
            if (group_counts.ContainsKey(e.element.group)) group_counts[e.element.group] += 1;
            else group_counts[e.element.group] = 1;
        }
        int group = utils.find_to_min(group_counts, (kv) => -kv.Value).Key;

        // Remove any entrypoints that aren't of the correct group
        for (int i = entries.Count - 1; i >= 0; --i)
            if (entries[i].element.group != group)
                entries.RemoveAt(i);

        if (characters_to_spawn == null)
        {
            // Generate an attack scaled to the town
            float combat_level = 0;
            foreach (var s in settler.get_settlers_by_group(group))
                combat_level += s.combat_level;

            List<string> to_spawn = new List<string>();
            var attackers = Resources.LoadAll<town_attacker>("characters/");
            while (combat_level > 0)
            {
                var att = attackers[Random.Range(0, attackers.Length)];
                if (att.character.combat_level > combat_level) break; // Would overshoot
                combat_level -= att.character.combat_level;
                to_spawn.Add(att.name);
            }
            characters_to_spawn = to_spawn;
        }

        // Distribute enemies evenly around the entrypoints
        foreach (var c in characters_to_spawn)
        {
            last_entrypoint_index = (last_entrypoint_index + 1) % entries.Count;
            entries[last_entrypoint_index].trigger_attack(c);
        }

        foreach (var s in settler.get_settlers_by_group(group))
            s.on_attack_begin();
    }

    public static void trigger_scaled_attack() => trigger_attack(null);
}
