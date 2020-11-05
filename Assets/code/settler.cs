using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IInspectable
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;

    public int group { get; private set; }
    protected override ICharacterController default_controller() { return new settler_control(); }

    List<settler_path_element> path;
    float delta_hunger = 0;
    float delta_tired = 0;

    public void Update()
    {
        // Look for my current assignment
        var assignment = settler_task_assignment.current_assignment(this);

        if (!has_authority)
        {
            if ((transform.position - assignment.transform.position).magnitude < 0.5f)
                assignment.interactable.on_interact(this);
            return;
        }

        // Authority-only control from here

        // Get hungry/tired
        delta_hunger += HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += TIREDNESS_PER_SECOND * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            hunger.value += 1;
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            tiredness.value += 1;
        }

        // Look for a new assignment if I don't have one
        if (assignment == null)
        {
            // Reset stuff
            path = null;

            // The next candidate interaction
            settler_interactable next = null;
            if (Random.Range(0, 100) < hunger.value)
                next = settler_interactable.random(settler_interactable.TYPE.EAT);

            else if (Random.Range(0, 100) < tiredness.value)
                next = settler_interactable.random(settler_interactable.TYPE.SLEEP);

            // Didn't need to do anything, so get to work
            if (next == null)
                next = settler_interactable.random(settler_interactable.TYPE.WORK);

            // No suitable interaction found
            if (next == null) return;

            // Create the assignment
            settler_task_assignment.try_assign(this, next);
            return;
        }

        // We have an assignment, attempt to carry it out

        // Check if we have a path
        if (path == null)
        {
            // Find a path to the assignment
            path = settler_path_element.path(transform.position,
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
            Vector3 forward = next_point - transform.position;
            forward.y = 0;
            if (forward.magnitude > 10e-3f) transform.forward = forward;

            if (utils.move_towards(transform, next_point,
                Time.deltaTime * walk_speed))
                path.RemoveAt(0);

            return;
        }

        // Carry out the assignment
        assignment.interactable.on_interact(this);

        if (assignment.interactable.is_complete(this))
        {
            // Assignment complete
            assignment.delete();
            return;
        }
    }

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

        if (element == null)
        {
            group = -1;
            return;
        }

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

class settler_control : ICharacterController
{
    public void control(character c) { }
    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
}
