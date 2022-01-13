using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IPlayerInteractable, ICanEquipArmour
{
    public const float TIME_TO_STARVE = 10f * 60f;
    public const float TIME_TO_REGEN = 120f;
    public const float TIME_TO_TIRED = time_manager.DAY_LENGTH;

    public List<Renderer> top_underclothes = new List<Renderer>();
    public List<Renderer> bottom_underclothes = new List<Renderer>();

    public Transform left_hand { get; private set; }
    public Transform right_hand { get; private set; }

    //###################//
    // CHARACTER CONTROL //
    //###################//

    protected override ICharacterController default_controller() { return null; }

    /// <summary> The path element that I am currently moving towards. </summary>
    public override town_path_element town_path_element
    {
        get
        {
            if (base.town_path_element == null)
                base.town_path_element = town_path_element.nearest_element_connected_to_beds(transform.position);
            return base.town_path_element;
        }
        set => base.town_path_element = value;
    }

    public int group => town_path_element == null ? -1 : town_path_element.group;
    public int room => town_path_element == null ? -1 : town_path_element.room;

    public void on_attack_begin()
    {
        // Stop interactions that aren't possible when under attack
        if (interaction?.skill.possible_when_under_attack == false)
            interaction.unassign();
    }

    protected override void on_death()
    {
        temporary_object.create(60f).gameObject.add_pinned_message("The settler " + name + " died!", Color.red);
        delete();
    }

    protected override bool create_dead_body() => false;

    public void look_at(Vector3 v, bool stay_upright = true)
    {
        Vector3 delta = v - transform.position;
        if (stay_upright) delta.y = 0;
        if (delta.magnitude < 0.001f) return;
        transform.forward = delta;
    }

    const byte GUARANTEED_FULL = 220;
    const byte GUARANTEED_EAT = 64;

    public bool ready_to_eat()
    {
        // Check needed things exist
        if (this == null) return false;
        if (nutrition == null) return false;

        int ms = nutrition.metabolic_satisfaction;

        // Not hungry
        if (ms > GUARANTEED_FULL) return false;
        if (ms > GUARANTEED_EAT)
        {
            float probability = ms - GUARANTEED_EAT;
            probability /= (GUARANTEED_FULL - GUARANTEED_EAT);
            probability = 1 - probability;
            if (probability < Random.Range(0, 1f)) return false;
        }

        // Don't eat if one of my friends is starving
        if (!starving && group_info.has_starvation(group))
            return false;

        return true;
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    protected override void Start()
    {
        // Get my left/right hand transforms
        foreach (var al in GetComponentsInChildren<armour_locator>())
            if (al.location == armour_piece.LOCATION.HAND)
            {
                switch (al.handedness)
                {
                    case armour_piece.HANDEDNESS.LEFT:
                        left_hand = al.transform;
                        break;

                    case armour_piece.HANDEDNESS.RIGHT:
                        right_hand = al.transform;
                        break;

                    case armour_piece.HANDEDNESS.EITHER:
                        throw new System.Exception("A hand has EITHER handedness!");
                }
            }

        settlers.Add(this);
    }

    protected override void Update()
    {
        if (controller != null)
        {
            // Under control by a character controller
            base.Update();
            return;
        }

        // Don't do anything if I'm interacting with players
        // Otherwise run my current interaction
        if (players_interacting_with.value > 0) return;
        interaction?.interact(this);

        GetComponentInChildren<facial_expression>().expression = current_expression();
    }

    facial_expression.EXPRESSION current_expression()
    {
        int total_mood = this.total_mood();
        if (Mathf.Abs(total_mood) <= 10) return facial_expression.EXPRESSION.NEUTRAL;
        if (total_mood < 0) return facial_expression.EXPRESSION.SAD;
        return facial_expression.EXPRESSION.HAPPY;
    }

    protected override void OnDestroy()
    {
        settlers.Remove(this);
    }

    //#################//
    // ICanEquipArmour //
    //#################//

    public armour_locator[] armour_locators() { return GetComponentsInChildren<armour_locator>(); }
    public float armour_scale() { return height_scale.value; }
    public Color hair_color() { return net_hair_color.value; }
    public bool armour_visible(armour_piece.LOCATION location) => true;

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public int hunger_percent()
    {
        return Mathf.RoundToInt(100f * (1f - nutrition.metabolic_satisfaction / (float)byte.MaxValue));
    }

    public int total_mood()
    {
        int ret = 0;
        foreach (var me in mood_effect.get_all(this)) ret += me.delta_mood;
        return ret;
    }

    player_interaction[] interactions;
    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        if (interactions == null) interactions = new player_interaction[]
        {
            new left_menu(this),
            new player_inspectable(transform)
            {
                text = () =>
                {
                    return name.capitalize() + "\n" +
                       "  " + "Combat level : " + combat_level + "\n" +
                       "  " + "Health " + remaining_health + "/" + max_health + "\n" +
                       "  " + Mathf.Round(tiredness.value) + "% tired\n" +
                       "  " + hunger_percent() + "% hungry\n" +
                       "  mood " + total_mood() + "\n" +
                       assignment_summary();
                }
            }
        };
        return interactions;
    }

    class left_menu : left_player_menu
    {
        color_selector hair_color_selector;
        color_selector top_color_selector;
        color_selector bottom_color_selector;
        UnityEngine.UI.Text info_panel_text;
        settler settler;

        public left_menu(settler settler) : base(settler.name) { this.settler = settler; }
        public override inventory editable_inventory() { return settler.inventory; }
        protected override RectTransform create_menu() { return settler.inventory.ui; }
        protected override void on_open()
        {
            settler.players_interacting_with.value += 1;

            foreach (var cs in settler.inventory.ui.GetComponentsInChildren<color_selector>(true))
            {
                if (cs.name.Contains("hair")) hair_color_selector = cs;
                else if (cs.name.Contains("top")) top_color_selector = cs;
                else bottom_color_selector = cs;
            }

            foreach (var tex in settler.inventory.ui.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (tex.name == "info_panel_text")
                {
                    info_panel_text = tex;
                    break;
                }

            info_panel_text.text = left_menu_text();

            hair_color_selector.color = settler.net_hair_color.value;
            top_color_selector.color = settler.top_color.value;
            bottom_color_selector.color = settler.bottom_color.value;

            hair_color_selector.on_change = () => settler.net_hair_color.value = hair_color_selector.color;
            top_color_selector.on_change = () => settler.top_color.value = top_color_selector.color;
            bottom_color_selector.on_change = () => settler.bottom_color.value = bottom_color_selector.color;
        }

        protected override void on_close()
        {
            settler.players_interacting_with.value -= 1;
        }

        string left_menu_text()
        {
            return settler.name.capitalize() + "\n" +
                   "Group " + settler.group + " room " + settler.room + "\n\n" +
                   "Health " + settler.remaining_health + "/" + settler.max_health + "\n" +
                   settler.tiredness.value + "% tired\n\n" +
                   settler.nutrition_info() + "\n\n" +
                   settler.mood_info() + "\n" +
                   settler.assignment_details();
        }
    }

    //#######################//
    // Formatted information //
    //#######################//

    public string mood_info()
    {
        string ret = "";
        int total_mood = 0;
        foreach (var me in mood_effect.get_all(this))
        {
            ret += "  " + me.display_name + " " + me.delta_mood + "\n";
            total_mood += me.delta_mood;
        }

        return "Mood " + total_mood + "\n" + ret;
    }

    public string assignment_summary()
    {
        var inter = interaction;
        if (inter == null) return "No assignment";

        return "Assignment:\n" +
            inter.task_summary().Trim() + "\n" +
            (inter.skill.is_visible ? inter.current_proficiency?.summary() : "");
    }

    public string assignment_details()
    {
        var inter = interaction;
        if (inter == null) return "No assignment";

        return "Assignment:\n" +
            inter.task_summary().Trim() + "\n" +
            (inter.skill.is_visible ? inter.current_proficiency?.breakdown() : "");
    }

    public string nutrition_info()
    {
        int max_length = 0;
        foreach (var g in food.all_groups)
            if (food.group_name(g).Length > max_length)
                max_length = food.group_name(g).Length;

        string ret = hunger_percent() + "% hungry\n";
        ret += "Diet satisfaction\n";
        foreach (food.GROUP g in food.all_groups)
        {
            string name = food.group_name(g).capitalize();
            while (name.Length < max_length) name += " ";
            ret += "  " + name + " " + Mathf.Round(nutrition[g] * 100f / 255f) + "%\n";
        }

        return ret.Trim();
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_food_satisfaction nutrition;
    public networked_variables.net_int tiredness;
    public networked_variables.net_string net_name;
    public networked_variables.net_int players_interacting_with;
    public networked_variables.net_color skin_color;
    public networked_variables.net_color top_color;
    public networked_variables.net_color net_hair_color;
    public networked_variables.net_color bottom_color;
    public networked_variables.net_job_priorities job_priorities;
    public networked_variables.net_skills skills;
    public networked_variables.net_float height_scale;

    public override float position_resolution() { return 0.1f; }
    public override float position_lerp_speed() { return 2f; }
    public override bool persistant() { return !is_dead; }
    public bool starving => nutrition.metabolic_satisfaction <= 0;
    public bool needs_sleep => tiredness.value > 80;

    public inventory inventory { get; private set; }

    /// <summary> The interactable object that we are currently interacting with. </summary>
    public settler_interactable interaction => settler_interactable.assigned_to(this);

    public void consume_food(food f)
    {
        nutrition.consume_food(f);
        foreach (var me in f.GetComponents<food_mood_effect>())
            add_mood_effect(me.effect.name);
    }

    public void add_mood_effect(string name)
    {
        // Can't add mood effects from non-auth client
        if (!has_authority) return;

        var effect = mood_effect.load(name);

        // Mood effect doesn't exist (mood_effect.load will flag a warning)
        if (effect == null) return;

        // Don't allow the same effect more than once
        foreach (var me in mood_effect.get_all(this)) 
            if (me.display_name == effect.display_name) return;

        client.create(transform.position, "mood_effects/" + name, parent: this);
    }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        nutrition = networked_variables.net_food_satisfaction.fully_satisfied;
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
        net_name = new networked_variables.net_string();
        skin_color = new networked_variables.net_color();
        top_color = new networked_variables.net_color();
        bottom_color = new networked_variables.net_color();
        net_hair_color = new networked_variables.net_color();
        height_scale = new networked_variables.net_float();
        players_interacting_with = new networked_variables.net_int();
        job_priorities = new networked_variables.net_job_priorities();
        skills = new networked_variables.net_skills();

        net_name.on_change = () => name = net_name.value;

        skin_color.on_change = () =>
        {
            foreach (var s in GetComponentsInChildren<skin>())
                s.color = skin_color.value;
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

        height_scale.on_change = () =>
        {
            transform.localScale = Vector3.one * height_scale.value;
            base.height = height_scale.value * 1.5f + 0.2f;
        };
    }

    public override void on_create()
    {
        players_interacting_with.value = 0;
    }

    public override void on_first_create()
    {
        base.on_first_create();
        net_name.value = names.random_unisex_name();
        skin_color.value = character_colors.random_skin_color();
        top_color.value = character_colors.clothing_brown;
        bottom_color.value = character_colors.clothing_brown;
        net_hair_color.value = character_colors.random_hair_color();
        height_scale.value = Random.Range(0.8f, 1.2f);

        foreach (var j in skill.all)
            skills.modify_xp(j, skill.level_to_xp(Random.Range(0, 10)));
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

    public override void on_gain_authority()
    {
        base.on_gain_authority();

        // Invoke repeating callbacks that need authority
        InvokeRepeating("get_hungry", TIME_TO_STARVE / byte.MaxValue, TIME_TO_STARVE / byte.MaxValue);
        InvokeRepeating("regen_health", TIME_TO_REGEN / max_health, TIME_TO_REGEN / max_health);
        InvokeRepeating("get_tired", TIME_TO_TIRED / 100f, TIME_TO_TIRED / 100f);
    }

    public override void on_loose_authority()
    {
        base.on_loose_authority();

        // Cancel repeating callbacks that need authority
        CancelInvoke("get_hungry");
        CancelInvoke("regen_health");
        CancelInvoke("get_tired");
    }

    void get_hungry()
    {
        nutrition.modify_every_satisfaction(-1);

        if (starving)
        {
            take_damage(1);
            foreach (var pm in gameObject.GetComponents<pinned_message>())
                if (pm.message.Contains("starving"))
                    return; // Already have starving message
            gameObject.add_pinned_message("The settler " + name + " is starving!", Color.red);
        }

        else
        {
            foreach (var pm in gameObject.GetComponents<pinned_message>())
                if (pm.message.Contains("starving"))
                    Destroy(pm); // Remove starving message
        }
    }

    void regen_health()
    {
        if (nutrition.metabolic_satisfaction < 100)
            return; // Don't heal if hungry
        else
            heal(1);
    }

    void get_tired()
    {
        tiredness.value += 1;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<settler> settlers;
    public static int settler_count => settlers.Count;
    new public static void initialize() => settlers = new HashSet<settler>();
    public static settler find_to_min(utils.float_func<settler> f) => utils.find_to_min(settlers, f);

    const float TIME_BETWEEN_SPAWNS = 5f;
    static float last_spawn_time = float.NegativeInfinity;

    public static List<settler> all_settlers()
    {
        var ret = new List<settler>(settlers);
        ret.Sort((a, b) => a.name.CompareTo(b.name));
        return ret;
    }

    public static HashSet<settler> get_settlers_by_group(int group)
    {
        HashSet<settler> ret = new HashSet<settler>();
        foreach (var s in settlers)
            if (s.group == group)
                ret.Add(s);
        return ret;
    }

    public static void try_spawn(int group, Vector3 location)
    {
        if (Time.time < last_spawn_time + TIME_BETWEEN_SPAWNS)
            return; // Wait until time to spawn again

        if (group_info.under_attack(group))
            return; // Don't spawn when under attack

        var set = get_settlers_by_group(group);
        if (set.Count >= group_info.bed_count(group)) return; // Not enough beds

        foreach (var s in set)
            if (s.nutrition.metabolic_satisfaction == 0)
                return; // Starvation => don't spawn

        last_spawn_time = Time.time;
        var spawned = client.create(location, "characters/settler") as settler;
        temporary_object.create(60f).gameObject.add_pinned_message(spawned.name.capitalize() + " has settled in the town!", Color.blue);
    }

    new public static string info()
    {
        return "    Total settler count : " + settlers.Count;
    }
}

//###########//
// ANIMATION //
//###########//

namespace settler_animations
{
    public abstract class animation : IArmController
    {
        private int last_frame_played;

        protected settler settler { get; private set; }
        protected arm left_arm { get; private set; }
        protected arm right_arm { get; private set; }
        protected Vector3 left_hand_pos;
        protected Vector3 right_hand_pos;
        protected float timer { get; private set; }

        public virtual void draw_arm_control_gizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(left_hand_pos, 0.025f);
            Gizmos.DrawWireSphere(right_hand_pos, 0.025f);
        }

        public animation(settler s)
        {
            settler = s;
            foreach (var a in s.GetComponentsInChildren<arm>())
            {
                Vector3 delta = a.transform.position - s.transform.position;
                if (Vector3.Dot(delta, s.transform.right) > 0) right_arm = a;
                else left_arm = a;
                a.controller = this;
            }
            last_frame_played = Time.frameCount;
        }

        public void play()
        {
            if (settler == null) return;
            left_arm.controller = this;
            right_arm.controller = this;
            timer += Time.deltaTime;
            last_frame_played = Time.frameCount;
            animate();
        }

        public bool arm_control_ended()
        {
            return Time.frameCount > last_frame_played + 1;
        }

        public void control_arm(arm arm)
        {
            if (arm == left_arm) arm.update_to_grab(left_hand_pos);
            else if (arm == right_arm) arm.update_to_grab(right_hand_pos);
            else Debug.LogError("Unkown arm!");
        }

        public virtual void draw_gizmos() { }

        protected abstract void animate();
    }

    public class simple_work : animation
    {
        float period;

        public simple_work(settler s, float period = 1f) : base(s)
        {
            this.period = period;
        }

        protected override void animate()
        {
            float min_dist = 0.3f * settler.height_scale.value;
            float max_dist = 0.5f * settler.height_scale.value;
            float range = max_dist - min_dist;

            float sin = Mathf.Sin(Mathf.PI * 2 * timer / period);
            Vector3 fw = settler.transform.forward;
            Vector3 left_delta = fw * (min_dist + range * (sin + 1f) / 2f);
            Vector3 right_delta = fw * (min_dist + range * (1f - sin) / 2f);

            left_delta -= settler.transform.up * settler.height_scale.value * 0.25f;
            right_delta -= settler.transform.up * settler.height_scale.value * 0.25f;

            left_hand_pos = left_arm.shoulder.position + left_delta;
            right_hand_pos = right_arm.shoulder.position + right_delta;
        }
    }
}