using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IInspectable
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;

    public int group { get; private set; }
    protected override ICharacterController default_controller() { return new settler_control_v2(); }

    new public string inspect_info()
    {
        return name.capitalize() + " (group " + group + ")\n" +
               "    " + Mathf.Round(hunger.value) + "% hungry\n" +
               "    " + Mathf.Round(tiredness.value) + "% tired";
    }

    private void Start()
    {
        update_group(snap: true);
        settlers.Add(this);
    }

    private void OnDestroy()
    {
        settlers.Remove(this);
    }

    void update_group(bool snap = false)
    {
        var element = settler_path_element.nearest_element(transform.position);
        if (snap) transform.position = element.transform.position;
        group = element.group;
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_int hunger;
    public networked_variables.net_int tiredness;
    public networked_variables.net_string net_name;
    public networked_variables.net_bool male;

    public override float position_resolution() { return 0.1f; }
    public override float position_lerp_speed() { return 2f; }
    public override bool persistant() { return true; }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();

        hunger = new networked_variables.net_int(min_value: 0, max_value: 100);
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
        net_name = new networked_variables.net_string();
        male = new networked_variables.net_bool();
        net_name.on_change = () => name = net_name.value;
    }

    public override void on_first_create()
    {
        base.on_first_create();
        male.value = Random.Range(0, 2) == 0;
        if (male.value) net_name.value = names.random_male_name();
        else net_name.value = names.random_female_name();
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<settler> settlers;

    new public static void initialize()
    {
        settlers = new HashSet<settler>();
    }

    public static void update_all_groups()
    {
        foreach (var s in settlers)
            s.update_group();
    }

    new public static string info()
    {
        return "    Total settler count : " + settlers.Count;
    }
}

class settler_control_v2 : ICharacterController
{
    List<settler_path_element> path;
    float interaction_time = 0;
    float delta_hunger = 0;
    float delta_tired = 0;

    public void control(character c)
    {
        var s = (settler)c;

        // Get hungry/tired
        delta_hunger += settler.HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += settler.TIREDNESS_PER_SECOND * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            s.hunger.value += 1;
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            s.tiredness.value += 1;
        }

        // Look for my current assignment
        var assignment = settler_task_assignment.current_assignment(s);

        // Look for a new assignment if I don't have one
        if (assignment == null)
        {
            // Reset stuff
            path = null;
            interaction_time = 0;

            // The next candidate interaction
            settler_interactable next = null;
            if (Random.Range(0, 100) < s.hunger.value)
                next = settler_interactable.random(settler_interactable.TYPE.EAT);

            else if (Random.Range(0, 100) < s.tiredness.value)
                next = settler_interactable.random(settler_interactable.TYPE.SLEEP);

            // Didn't need to do anything, so get to work
            if (next == null)
                next = settler_interactable.random(settler_interactable.TYPE.WORK);

            // No suitable interaction found
            if (next == null) return;

            // Create the assignment
            settler_task_assignment.try_assign(s, next);
            return;
        }

        // We have an assignment, attempt to carry it out

        // Check if we have a path
        if (path == null)
        {
            // Find a path to the assignment
            path = settler_path_element.path(s.transform.position,
                assignment.interactable.path_element);

            if (path == null)
            {
                // Couldn't path to assignment, delete it
                assignment.delete();
                return;
            }
        }

        // Check if there is any of the path left to walk
        if (path.Count > 0)
        {
            if (path[0] == null)
            {
                // Path has been destroyed, reset
                path = null;
                return;
            }

            // Walk the path to completion
            Vector3 next_point = path[0].transform.position;
            Vector3 forward = next_point - c.transform.position;
            forward.y = 0;
            if (forward.magnitude > 10e-3f) c.transform.forward = forward;

            if (utils.move_towards(s.transform, next_point,
                Time.deltaTime * s.walk_speed))
                path.RemoveAt(0);

            return;
        }

        // Carry out the assignment
        interaction_time += Time.deltaTime;
        if (assignment.interactable.interact(s, interaction_time))
        {
            // Assignment complete
            assignment.delete();
            return;
        }
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
}

class settler_control : ICharacterController
{
    List<settler_path_element> path;
    settler_interactable target;
    float walk_speed_mod = 1f;
    bool interaction_complete = false;
    float interaction_time = 0;

    float delta_hunger = 0;
    float delta_tired = 0;

    public void control(character c)
    {
        var s = (settler)c;

        // Get hungry/tired
        delta_hunger += settler.HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += settler.TIREDNESS_PER_SECOND * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            s.hunger.value += 1;
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            s.tiredness.value += 1;
        }

        if (target == null)
        {
            // Set the initial target to the nearest one and teleport there
            target = settler_interactable.nearest(settler_interactable.TYPE.WORK, c.transform.position);
            if (target == null || target.path_element == null) return;
            c.transform.position = target.path_element.transform.position;
        }

        if (path != null && path.Count > 0)
        {
            if (path[0] == null)
            {
                // Path has been destroyed, reset
                path = null;
                target = null;
                return;
            }

            // Walk the path to completion  
            Vector3 next_point = path[0].transform.position;
            Vector3 forward = next_point - c.transform.position;
            forward.y = 0;
            if (forward.magnitude > 10e-3f) c.transform.forward = forward;

            if (utils.move_towards(c.transform, next_point,
                Time.deltaTime * c.walk_speed * walk_speed_mod))
                path.RemoveAt(0);

            return;
        }

        if (interaction_complete || target.interact(s, interaction_time))
        {
            interaction_complete = true;

            // The next candidate interaction
            settler_interactable next = null;
            if (Random.Range(0, 100) < s.hunger.value)
                next = settler_interactable.random(settler_interactable.TYPE.EAT);

            else if (Random.Range(0, 100) < s.tiredness.value)
                next = settler_interactable.random(settler_interactable.TYPE.SLEEP);

            // Didn't need to do anything, so get to work
            if (next == null)
                next = settler_interactable.random(settler_interactable.TYPE.WORK);

            // Don't consider null interactables
            // or returning to the same target
            if (next == null) return;
            if (next == target) return;

            // Attempt to path to new target
            path = settler_path_element.path(target.path_element, next.path_element);

            if (path == null)
            {
                // Pathing failed
                return;
            }

            // Pathing success, this is our next target
            target = next;

            // Reset things
            interaction_complete = false;
            interaction_time = 0;

            // Set a new randomized path speed, so settlers 
            // don't end up 100% in-phase
            walk_speed_mod = Random.Range(0.9f, 1.1f);
        }
        else
        {
            // Increment the interaction timer
            interaction_time += Time.deltaTime;
        }
    }

    public void draw_gizmos()
    {
        Gizmos.color = Color.green;

        if (target != null)
            Gizmos.DrawWireSphere(target.transform.position, 0.2f);

        if (path != null)
            for (int i = 1; i < path.Count; ++i)
                Gizmos.DrawLine(
                    path[i].transform.position + Vector3.up,
                    path[i - 1].transform.position + Vector3.up);
    }

    public void draw_inspector_gui()
    {
#if UNITY_EDITOR
        string text = target == null ? "No target\n" : "Target = " + target.name + "\n";
        text += (path == null) ? "No path\n" : "Path length = " + path.Count;
        UnityEditor.EditorGUILayout.TextArea(text);
#endif
    }
}
