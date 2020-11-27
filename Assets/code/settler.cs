using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IInspectable, ILeftPlayerMenu, ICanEquipArmour
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;

    public List<Renderer> skin_renderers = new List<Renderer>();
    public List<Renderer> top_underclothes = new List<Renderer>();
    public List<Renderer> bottom_underclothes = new List<Renderer>();

    public int group { get; private set; }
    protected override ICharacterController default_controller() { return new settler_control(); }

    List<settler_path_element> path;
    float delta_hunger = 0;
    float delta_tired = 0;
    settler_task_assignment assignment;

    public void Update()
    {
        // Don't do anything if there is a player interacting with me
        if (players_interacting_with.value > 0)
            return;

        // Look for my current assignment
        assignment = settler_task_assignment.current_assignment(this);

        if (!has_authority)
        {
            if (assignment != null)
            {
                // Mimic assignment on non-authority client
                if ((transform.position - assignment.transform.position).magnitude < 0.5f)
                    assignment.interactable.on_interact(this);
            }
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

            // Get an eat task
            if (Random.Range(20, 100) < hunger.value &&
                settler_task_assignment.try_assign(this,
                settler_interactable.proximity_weighted_ramdon(
                    settler_interactable.TYPE.EAT, transform.position)))
                return;

            // Get a sleep task
            if (Random.Range(20, 100) < tiredness.value &&
                settler_task_assignment.try_assign(this,
                settler_interactable.proximity_weighted_ramdon(
                    settler_interactable.TYPE.SLEEP, transform.position)))
                return;

            // Get a work task
            if (settler_task_assignment.try_assign(this,
                settler_interactable.proximity_weighted_ramdon(
                    settler_interactable.TYPE.WORK, transform.position)))
                return;

            // No suitable interaction found
            return;
        }

        // Wait for assignment to be registered
        if (assignment.network_id < 0)
            return;

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

            if (path.Count > 1)
            {
                // Gradually turn towards the next direction, to make
                // going round sharp corners look natural
                Vector3 next_next_point = path[1].transform.position;
                Vector3 next_forward = next_next_point - next_point;

                float w1 = (transform.position - next_point).magnitude;
                float w2 = (transform.position - next_next_point).magnitude;
                forward = forward.normalized * w1 + next_forward.normalized * w2;
                forward /= (w1 + w2);
            }

            forward.y = 0;
            if (forward.magnitude > 10e-3f)
            {
                // If we need to do > 90 degree turn, just do it instantly
                if (Vector3.Dot(transform.forward, forward) < 0)
                    transform.forward = forward;
                else // Otherwise, lerp our forward vector
                    transform.forward = Vector3.Lerp(transform.forward, forward, Time.deltaTime * 5f);
            }

            if (utils.move_towards(transform, next_point,
                Time.deltaTime * walk_speed, arrive_distance: 0.25f))
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
        string ass_string = "No assignment.";
        if (assignment != null)
            ass_string = "Assignment: " + assignment.interactable.task_info();

        return name.capitalize() + " (group " + group + ")\n" +
               "    " + Mathf.Round(hunger.value) + "% hungry\n" +
               "    " + Mathf.Round(tiredness.value) + "% tired\n" +
               ass_string;
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

    //#################//
    // ICanEquipArmour //
    //#################//

    public armour_locator[] armour_locators() { return GetComponentsInChildren<armour_locator>(); }
    public float armour_scale() { return height.value; }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public string left_menu_display_name() { return name; }
    public inventory editable_inventory() { return inventory; }
    public RectTransform left_menu_transform() { return inventory.ui; }
    public void on_left_menu_close() { players_interacting_with.value -= 1; }
    public void on_left_menu_open() { players_interacting_with.value += 1; }

    public recipe[] additional_recipes() { return null; }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_int hunger;
    public networked_variables.net_int tiredness;
    public networked_variables.net_string net_name;
    public networked_variables.net_bool male;
    public networked_variables.net_int players_interacting_with;
    public networked_variables.net_color skin_color;
    public networked_variables.net_color top_color;
    public networked_variables.net_color bottom_color;
    new public networked_variables.net_float height;

    public override float position_resolution() { return 0.1f; }
    public override float position_lerp_speed() { return 2f; }
    public override bool persistant() { return true; }

    public inventory inventory { get; private set; }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        hunger = new networked_variables.net_int(min_value: 0, max_value: 100);
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
        net_name = new networked_variables.net_string();
        male = new networked_variables.net_bool();
        skin_color = new networked_variables.net_color();
        top_color = new networked_variables.net_color();
        bottom_color = new networked_variables.net_color();
        height = new networked_variables.net_float();
        players_interacting_with = new networked_variables.net_int();

        net_name.on_change = () => name = net_name.value;

        skin_color.on_change = () =>
        {
            foreach (var r in skin_renderers)
                utils.set_color(r.material, skin_color.value);
        };

        top_color.on_change = () =>
        {
            foreach (var r in top_underclothes)
                utils.set_color(r.material, top_color.value);
        };

        bottom_color.on_change = () =>
        {
            foreach (var r in bottom_underclothes)
                utils.set_color(r.material, bottom_color.value);
        };

        height.on_change = () =>
        {
            transform.localScale = Vector3.one * height.value;
        };
    }

    public override void on_first_create()
    {
        base.on_first_create();
        male.value = Random.Range(0, 2) == 0;
        if (male.value) net_name.value = names.random_male_name();
        else net_name.value = names.random_female_name();
        skin_color.value = character_colors.random_skin_color();
        top_color.value = Random.ColorHSV();
        bottom_color.value = Random.ColorHSV();
        height.value = Random.Range(0.8f, 1.2f);
    }

    public override void on_first_register()
    {
        base.on_first_register();
        var inv = (inventory)client.create(transform.position, "inventories/settler_inventory", parent: this);

        // Randomize clothing
        inv.add_register_listener(() =>
        {
            var armour_slots = inv.ui.GetComponentsInChildren<armour_slot>();

            // Choose the armour slots to fill
            var locations_to_fill = new HashSet<armour_piece.LOCATION>();
            foreach (var slot in armour_slots)
                if (Random.Range(0, 3) == 0)
                    locations_to_fill.Add(slot.location);
            
            // Fill the chosen armour slots
            var armours = Resources.LoadAll<armour_piece>("items");
            foreach (var slot in armour_slots)
            {
                if (!locations_to_fill.Contains(slot.location))
                    continue;

                List<armour_piece> options = new List<armour_piece>();
                foreach (var a in armours)
                    if (slot.accepts(a))
                        options.Add(a);

                if (options.Count == 0)
                    continue;

                var chosen = options[Random.Range(0, options.Count)];
                inv.set_slot(slot, chosen.name, 1);
            }
        });
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
            inventory = (inventory)child;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<settler> settlers;

    public static int settler_count => settlers.Count;

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
