using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class attacker_entrypoint : MonoBehaviour, INonEquipable, INonBlueprintable, IExtendsNetworked, IAddsToInspectionText
{
    public const float MIN_EXTERNAL_PATH_DISTANCE = 15f;
    public const float MAX_EXTERNAL_PATH_DISTANCE = 25f;

    const float MIN_TIME_BETWEEN_PATHS = 1f;
    const float MAX_TIME_BETWEEN_PATHS = 5f;

    path path;
    float next_path_time = float.NegativeInfinity;
    float time_between_paths = MIN_TIME_BETWEEN_PATHS;
    bool geometry_changed_since_last_path = false;

    public bool path_complete => path is explicit_path;
    public Vector3 path_end => path == null ? transform.position : path[path.length - 1];
    public town_path_element element => GetComponentInParent<town_path_element>();
    public bool has_authority => GetComponentInParent<networked>()?.has_authority == true;

    /// <summary> The region where paths from this entrypoint might exist. </summary>
    public Bounds reachable_region => new Bounds(transform.position, Vector3.one * 2 * MAX_EXTERNAL_PATH_DISTANCE);

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (path == null)
            return "No entrypoint path";

        if (path is explicit_path)
            return "Entrypoint path complete";

        return "Entrypoint pathing underway";
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start() => entrypoints.Add(this);
    private void OnDestroy() => entrypoints.Remove(this);

    void on_geometry_change_within_reachable_region()
    {
        geometry_changed_since_last_path = true;
    }

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
            if (!geometry_changed_since_last_path) return; // No point trying again until the geometry updates
            if (Time.time < next_path_time) return; // Don't try again until the next path time

            geometry_changed_since_last_path = false;

            path = new random_path(transform.position, new agent(), endpoint_success,
                midpoint_successful: midpoint_success,
                starting_direction: element.out_of_town_direction);
        }

        if (path is explicit_path)
        {
            if (geometry_changed_since_last_path)
            {
                if (!path.validate(load_balancing.iter, out bool cycle_complete))
                    path = null; // Path invalidated
                if (cycle_complete)
                    geometry_changed_since_last_path = false; // We've checked the full path since last geometry change
            }
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
                            return;
                        }
                    }

                    // Fallback, keep whole path
                    List<Vector3> whole_path = new List<Vector3>();
                    for (int i = 0; i < path.length; ++i) whole_path.Add(path[i]);
                    path = new explicit_path(whole_path, new agent());
                    refresh_drawn_path();
                    return;
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

    private void OnDrawGizmos() => path?.draw_gizmos();

    //#########//
    // PATHING //
    //#########//

    // Is the vector v considered to be on the terrain
    bool point_on_terrain(Vector3 v)
    {
        var terr = utils.raycast_for_closest<Terrain>(
            new Ray(v + 0.1f * Vector3.up, Vector3.down),
            out RaycastHit hit, max_distance: 1.1f);
        return terr != null;
    }

    bool midpoint_success(Vector3 v) => (transform.position - v).magnitude > MAX_EXTERNAL_PATH_DISTANCE && point_on_terrain(v);
    bool endpoint_success(Vector3 v) => (transform.position - v).magnitude > MIN_EXTERNAL_PATH_DISTANCE && point_on_terrain(v);

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
            pathfinding_utils.validate_walking_move(a, b, resolution, height);
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
        public virtual string inspect_info() => "Attacking town.";
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
                case STAGE.PATHING_AWAY: return "Pathfinding ()";
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
        if (!nw.has_authority) return; // Only trigger attacks on auth client

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

    static attacker_entrypoint()
    {
        string help_text =
            "A town is defined as a collection of buildings that are connected by paths. " +
            "Only certain buildings can connect to the path network. The paths will only be " +
            "shown if you have a connectable building equipped - this is known as \"path-view\" mode.\n\n" +
            "Entrypoints to the town will be automatically generated and are shown as " +
            "red paths going off into the distance in path-view mode. These are the " +
            "routes that visitors (but also enemies!) will use to enter the town.\n\n" +
            "Entrypoints can be blocked off by placing buildings in the way, allowing you " +
            "to build walls to protect your town from attack. But be careful - a town with no " +
            "entrypoints is doomed to fail.\n\n" +
            "Settlers will move into towns automatically if there are enough beds connected - " +
            "build a town gate and inspect it with " + controls.bind_name(controls.BIND.INSPECT) +
            " to see how many connected beds/settlers there are.\n\n" +
            "Towns are seperated into rooms - these can be identified by inspecting connected " +
            "buildings with " + controls.bind_name(controls.BIND.INSPECT) + ". " +
            "Rooms are seperated by certain objects such as doors, gates and ladders. " +
            "The function of a room depends on the buildings connected to that room. For example, " +
            "connecting a forge designates that room as a foundry. Inpect the forge to see what other " +
            "buildings are needed to make a working foundry.";

        help_book.add_entry("towns", help_text);

        // Add geometry change listener
        world.add_geometry_change_listener((regions) =>
        {
            foreach (var ep in entrypoints)
                foreach (var r in regions)
                    if (ep.reachable_region.Intersects(r))
                    {
                        ep.on_geometry_change_within_reachable_region();
                        break;
                    }
        });
    }

    static pinned_message attack_message;
    static HashSet<attacker_entrypoint> entrypoints;
    static Dictionary<int, HashSet<character>> attackers;
    static int last_entrypoint_index = 0;

    public static bool attacks_enabled;

    public static List<attacker_entrypoint> valid_entrypoints(int group = -1)
    {
        var ret = new List<attacker_entrypoint>();
        foreach (var e in entrypoints)
        {
            if (e == null || e.element == null) continue;
            if (group >= 0 && e.element.group != group) continue;
            if (!e.path_complete) continue;
            ret.Add(e);
        }
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
                    if (a == null || a.is_dead)
                        continue;

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

    static float character_distance_cost(character c, Vector3 position)
    {
        if (c.is_dead) return Mathf.Infinity;
        return (c.transform.position - position).sqrMagnitude;
    }

    public static character closest_attacker(Vector3 position, int group = -1)
    {
        if (group >= 0)
        {
            if (!attackers.TryGetValue(group, out HashSet<character> cs))
                return null;
            return utils.find_to_min(cs, (c) => character_distance_cost(c, position));
        }

        var all_closest = new List<character>();
        foreach (var kv in attackers)
        {
            var closest = utils.find_to_min(kv.Value, (c) => character_distance_cost(c, position));
            if (closest != null)
                all_closest.Add(closest);
        }
        return utils.find_to_min(all_closest, (c) => character_distance_cost(c, position));
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
        {
            if (e.path == null) continue;
            if (e.path.state != path.STATE.COMPLETE) continue;
            if (!group_info.has_beds(e.element.group)) continue;
            entries.Add(e);
        }
        if (entries.Count == 0) return; // No entrypoints for an attack

        if (characters_to_spawn == null)
        {
            // Generate an attack scaled to the town
            float combat_level = 0;
            foreach (var s in settler.all_settlers())
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

        foreach (var s in settler.all_settlers())
            s.on_attack_begin();
    }

    public static void trigger_scaled_attack() => trigger_attack(null);
}
