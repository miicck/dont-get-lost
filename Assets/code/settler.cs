using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IInspectable, ILeftPlayerMenu, ICanEquipArmour
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float HEAL_RATE = 100f / 120f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;
    public const byte MAX_METABOLIC_SATISFACTION_TO_EAT = 220;
    public const byte GUARANTEED_EAT_METABOLIC_SATISFACTION = 64;

    public List<Renderer> skin_renderers = new List<Renderer>();
    public List<Renderer> top_underclothes = new List<Renderer>();
    public List<Renderer> bottom_underclothes = new List<Renderer>();

    //###################//
    // CHARACTER CONTROL //
    //###################//

    protected override ICharacterController default_controller() { return new settler_control(); }

    class settler_control : ICharacterController
    {
        public void control(character c)
        {
            var s = (settler)c;
            s.default_control();
        }

        public void on_end_control(character c) { }
        public void draw_gizmos() { }
        public void draw_inspector_gui() { }

        public string inspect_info()
        {
            return "";
        }
    }

    settler_path_element.path path;

    /// <summary> The path element that I am currently moving 
    /// towards. </summary>
    settler_path_element path_element
    {
        get => _path_element;
        set
        {
            if (_path_element == value)
                return;

            _path_element?.on_settler_leave(this);
            _path_element = value;
            _path_element?.on_settler_enter(this);
        }
    }
    settler_path_element _path_element;

    public int group => path_element == null ? -1 : path_element.group;
    float delta_hunger = 0;
    float delta_tired = 0;
    float delta_heal = 0;
    settler_task_assignment assignment;

    void default_control()
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

        // Get hungry/tired/heal
        delta_hunger += HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += TIREDNESS_PER_SECOND * Time.deltaTime;

        delta_heal += HEAL_RATE * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            nutrition.modify_every_satisfaction(-1);
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            tiredness.value += 1;
        }

        if (delta_heal > 1f)
        {
            delta_heal = 0f;
            heal(1);
        }

        // Look for a new assignment if I don't have one
        if (assignment == null)
        {
            // Reset stuff
            path = null;

            // Get an eat task
            if (Random.Range(
                GUARANTEED_EAT_METABOLIC_SATISFACTION,
                MAX_METABOLIC_SATISFACTION_TO_EAT) >
                nutrition.metabolic_satisfaction &&
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

            // Get a guard task
            if (town_gate.group_under_attack(group))
            {
                if (settler_task_assignment.try_assign(this,
                    settler_interactable.proximity_weighted_ramdon(
                        settler_interactable.TYPE.GUARD, transform.position)))
                    return;

                // Flee task here?
            }

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
            path_element = settler_path_element.nearest_element(transform.position);
            path = new settler_path_element.path(path_element, assignment.interactable.path_element);

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
            settler_path_element element_walking_towards;
            if (path.walk(transform, assignment.interactable.move_to_speed(this), out element_walking_towards))
                path = null;
            path_element = element_walking_towards;
            path_element?.on_settler_move_towards(this);
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

    protected override void on_death()
    {
        temporary_object.create(60f).gameObject.add_pinned_message("The settler " + name + " died!", Color.red);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start()
    {
        settlers.Add(this);
    }

    private void OnDestroy()
    {
        path_element = null;
        settlers.Remove(this);
    }

    private void OnDrawGizmos()
    {
        if (path_element == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(path_element.transform.position, 0.1f);
    }

    //##############//
    // IINspectable //
    //##############//

    new public string inspect_info()
    {
        string ass_string = "No assignment.";
        if (assignment != null)
            ass_string = "Assignment: " + assignment.interactable.task_info();

        return name.capitalize() + " (group " + group + ")\n" +
               "    " + "Health " + remaining_health + "/" + max_health + "\n" +
               "    " + Mathf.Round(tiredness.value) + "% tired\n" +
               "    " + Mathf.Round(nutrition.hunger * 100f / 255f) + "% hungry\n" +
               ass_string + "\n" +
               "    interacting with " + players_interacting_with.value + " players";
    }

    //#################//
    // ICanEquipArmour //
    //#################//

    public armour_locator[] armour_locators() { return GetComponentsInChildren<armour_locator>(); }
    public float armour_scale() { return height.value; }
    public Color hair_color() { return net_hair_color.value; }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public string left_menu_display_name() { return name; }
    public inventory editable_inventory() { return inventory; }
    public RectTransform left_menu_transform() { return inventory.ui; }

    color_selector hair_color_selector;
    color_selector top_color_selector;
    color_selector bottom_color_selector;
    UnityEngine.UI.Text info_panel_text;

    public void on_left_menu_close()
    {
        players_interacting_with.value -= 1;
    }

    public void on_left_menu_open()
    {
        players_interacting_with.value += 1;

        foreach (var cs in inventory.ui.GetComponentsInChildren<color_selector>(true))
        {
            if (cs.name.Contains("hair")) hair_color_selector = cs;
            else if (cs.name.Contains("top")) top_color_selector = cs;
            else bottom_color_selector = cs;
        }

        foreach (var tex in inventory.ui.GetComponentsInChildren<UnityEngine.UI.Text>())
            if (tex.name == "info_panel_text")
            {
                info_panel_text = tex;
                break;
            }

        info_panel_text.text = left_menu_text();

        hair_color_selector.color = net_hair_color.value;
        top_color_selector.color = top_color.value;
        bottom_color_selector.color = bottom_color.value;

        hair_color_selector.on_change = () => net_hair_color.value = hair_color_selector.color;
        top_color_selector.on_change = () => top_color.value = top_color_selector.color;
        bottom_color_selector.on_change = () => bottom_color.value = bottom_color_selector.color;
    }

    public string left_menu_text()
    {
        return name.capitalize() + "\n\n" +
               "Health " + remaining_health + "/" + max_health + "\n" +
               tiredness.value + "% tired\n" +
               "Group  " + group + "\n\n" +
               nutrition_info();
    }

    public string nutrition_info()
    {
        int max_length = 0;
        foreach (var g in food.all_groups)
            if (food.group_name(g).Length > max_length)
                max_length = food.group_name(g).Length;

        string ret = Mathf.Round(nutrition.hunger * 100f / 255f) + "% hungry\n";
        ret += "Diet satisfaction\n";
        foreach (food.GROUP g in food.all_groups)
        {
            string name = food.group_name(g).capitalize();
            while (name.Length < max_length) name += " ";
            ret += "  " + name + " " + Mathf.Round(nutrition[g] * 100f / 255f) + "%\n";
        }

        return ret;
    }

    public recipe[] additional_recipes() { return null; }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_food_satisfaction nutrition;
    public networked_variables.net_int tiredness;
    public networked_variables.net_string net_name;
    public networked_variables.net_bool male;
    public networked_variables.net_int players_interacting_with;
    public networked_variables.net_color skin_color;
    public networked_variables.net_color top_color;
    public networked_variables.net_color net_hair_color;
    public networked_variables.net_color bottom_color;
    new public networked_variables.net_float height;

    public override float position_resolution() { return 0.1f; }
    public override float position_lerp_speed() { return 2f; }
    public override bool persistant() { return true; }

    public inventory inventory { get; private set; }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        nutrition = new networked_variables.net_food_satisfaction();
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
        net_name = new networked_variables.net_string();
        male = new networked_variables.net_bool();
        skin_color = new networked_variables.net_color();
        top_color = new networked_variables.net_color();
        bottom_color = new networked_variables.net_color();
        net_hair_color = new networked_variables.net_color();
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

        net_hair_color.on_change = () =>
        {
            // Set hair color
            foreach (var al in armour_locators())
                if (al.equipped != null && al.equipped is hairstyle)
                    al.equipped.on_equip(this);
        };

        height.on_change = () =>
        {
            transform.localScale = Vector3.one * height.value;
            base.height = height.value * 1.5f + 0.2f;
        };
    }

    public override void on_create()
    {
        players_interacting_with.value = 0;
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
        net_hair_color.value = Random.ColorHSV();
        height.value = Random.Range(0.8f, 1.2f);
    }

    float armour_location_fill_probability(armour_piece.LOCATION loc)
    {
        switch (loc)
        {
            case armour_piece.LOCATION.HEAD: return 0.9f;
            default: return 0.25f;
        }
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
                if (Random.Range(0, 1f) < armour_location_fill_probability(slot.location))
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

    public static settler find_to_min(utils.float_func<settler> f)
    {
        return utils.find_to_min(settlers, f);
    }

    new public static string info()
    {
        return "    Total settler count : " + settlers.Count;
    }
}
