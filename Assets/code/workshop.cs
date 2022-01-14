using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workshop : settler_interactable_options, IAddsToInspectionText
{
    static workshop()
    {
        help_book.add_entry("Workshops",
            "Workshops are rooms containing a collection of useful " +
            "tools for a specific purpose. A specific object " +
            "(e.g a forge) designates the type of workshop " +
            "(e.g a smithing workshop). This object can be inspected " +
            "to see what other objects need to be placed in the room " +
            "for it to become operational (e.g an anvil for a forge).\n\n" +
            "Once a workshop has become operational, it must be suplied " +
            "with materials. These materials can be fed into dispensers " +
            "(e.g a materials cupboard), which must be connected to the room."
        );
    }


    public string crafting_options_title = "crafting";
    public float base_craft_time = 10f;
    new public town_path_element path_element;
    public item_output product_output;
    public List<building_material> required_fixtures;
    Dictionary<dispenser, Dictionary<string, int>> to_pickup;

    List<fixture> found_fixtures;
    class fixture
    {
        public fixture(building_material building, town_path_element.path path)
        {
            this.building = building;
            this.path = path;
        }

        public building_material building { get; private set; }
        public town_path_element.path path { get; private set; }
    }

    List<dispenser> dispensers = new List<dispenser>();
    class dispenser : IItemCollection
    {
        public dispenser(item_dispenser item_dispenser, town_path_element.path path)
        {
            this.item_dispenser = item_dispenser;
            this.path = path;
        }

        public item_dispenser item_dispenser { get; private set; }
        public town_path_element.path path { get; private set; }

        public Dictionary<item, int> contents() => item_dispenser.contents();
        public bool add(item i, int c) => item_dispenser.add(i, c);
        public bool remove(item i, int c) => item_dispenser.remove(i, c);
    }

    //##############################//
    // settler_interactable_options //
    //##############################//

    recipe[] recipes
    {
        get
        {
            if (_recipes == null)
                _recipes = Resources.LoadAll<recipe>("recipes/workshops/" + name);
            return _recipes;
        }
    }
    recipe[] _recipes;

    recipe current_recipe
    {
        get
        {
            if (recipes.Length == 0)
                throw new System.Exception("No recipes for workshop: " + name);
            return recipes[selected_option];
        }
    }

    protected override int options_count => recipes.Length;
    protected override string options_title => crafting_options_title;

    protected override option get_option(int i)
    {
        return new option
        {
            text = recipes[i].craft_string(),
            sprite = recipes[i].products[0].sprite()
        };
    }

    public override string task_summary()
    {
        string prod = current_recipe.products[0].product_name();
        string loc = GetComponentInParent<building_material>().display_name;
        return "making " + utils.a_or_an(prod) + " " + prod + " at " + utils.a_or_an(loc) + " " + loc;
    }

    protected override bool ready_to_assign(settler s)
    {
        if (!validate_fixtures()) return false;
        if (!validate_recipe()) return false;
        return true;
    }

    bool walking_to_dispenser = true;
    float craft_timer = 0;
    float total_timer = 0;
    settler_animations.simple_work work_anim;
    Dictionary<dispenser, Dictionary<string, int>> to_pickup_this_cycle;

    protected override void on_arrive(settler s)
    {
        // Reset things
        walking_to_dispenser = true;
        craft_timer = 0;
        total_timer = 0;
        work_anim = null;
        to_pickup_this_cycle = null;
    }

    STAGE_RESULT prepare_to_gather_materials(settler s)
    {
        // Don't do anything on non-auth clients
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Don't know what to gather
        if (to_pickup == null) return STAGE_RESULT.TASK_FAILED;

        // Copy the to_pickup dictionary for this cycle
        to_pickup_this_cycle = new Dictionary<dispenser, Dictionary<string, int>>();
        foreach (var kv in to_pickup)
        {
            to_pickup_this_cycle[kv.Key] = new Dictionary<string, int>();
            foreach (var kv2 in kv.Value)
                to_pickup_this_cycle[kv.Key][kv2.Key] = kv2.Value;
        }

        return STAGE_RESULT.STAGE_COMPLETE;
    }

    STAGE_RESULT gather_materials(settler s)
    {
        // Don't do anything on non-auth clients
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Don't know what to gather
        if (to_pickup_this_cycle == null) return STAGE_RESULT.TASK_FAILED;

        // Loop over items to pickup
        foreach (var kv in to_pickup_this_cycle)
            switch (kv.Key.path.walk(s, s.walk_speed, forwards: walking_to_dispenser))
            {
                case town_path_element.path.WALK_STATE.COMPLETE:
                    if (walking_to_dispenser)
                    {
                        // Pickup materials from the dispenser
                        walking_to_dispenser = false;
                        foreach (var ikv in kv.Value)
                            kv.Key.remove(ikv.Key, ikv.Value);
                    }
                    else
                    {
                        // Got back with materials - remove this entry from the dict
                        walking_to_dispenser = true;
                        to_pickup_this_cycle.Remove(kv.Key);
                    }
                    return STAGE_RESULT.STAGE_UNDERWAY;

                case town_path_element.path.WALK_STATE.UNDERWAY:
                    return STAGE_RESULT.STAGE_UNDERWAY;

                default:
                    return STAGE_RESULT.TASK_FAILED;
            }

        // Completed the gather-and-return stage
        return STAGE_RESULT.STAGE_COMPLETE;
    }

    STAGE_RESULT prepare_to_craft(settler s)
    {
        // Don't do anything else on non-auth clients
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        craft_timer = 0;
        return STAGE_RESULT.STAGE_COMPLETE;
    }

    STAGE_RESULT craft(settler s)
    {
        // Play the work animation
        if (work_anim == null)
        {
            s.look_at(transform.position);
            work_anim = new settler_animations.simple_work(
                s, 1f / current_proficiency.total_multiplier);
        }
        work_anim.play();

        // Don't do anything else on non-auth clients
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Increment crafting timer/eventually complete the crafting stage
        craft_timer += Time.deltaTime * current_proficiency.total_multiplier;
        if (craft_timer < base_craft_time) return STAGE_RESULT.STAGE_UNDERWAY;
        return STAGE_RESULT.STAGE_COMPLETE;
    }

    STAGE_RESULT create_output(settler s)
    {
        foreach (var p in current_recipe.products)
            p.create_in_node(product_output, track_production: true);

        // Don't do anything else on non-auth clients
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Keep cycling until total time hits 60 seconds
        return total_timer > 60 ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_COMPLETE;
    }

    delegate STAGE_RESULT stage_function(settler s);

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Record total time
        if (s.has_authority) total_timer += Time.deltaTime;

        stage_function[] stages = new stage_function[]
        {
            prepare_to_gather_materials,
            gather_materials,
            prepare_to_craft,
            craft,
            create_output
        };

        // Delegate interaction to approprate stage
        return stages[stage % stages.Length](s);
    }

    bool validate_recipe()
    {
        // Check if crafting is possible
        if (!current_recipe.can_craft(dispensers, out Dictionary<IItemCollection, Dictionary<string, int>> found))
            return false;

        // Remember where to pick up each of the ingredients
        to_pickup = new Dictionary<dispenser, Dictionary<string, int>>();
        foreach (var kv in found) to_pickup[(dispenser)kv.Key] = kv.Value;
        return true;
    }

    bool validate_fixtures()
    {
        if (found_fixtures == null)
        {
            // Initialize the found_fixtures to all null
            found_fixtures = new List<fixture>(required_fixtures.Count);
            for (int i = 0; i < required_fixtures.Count; ++i)
                found_fixtures.Add(null);
        }

        // Remove dead dispensers
        for (int i = dispensers.Count - 1; i >= 0; --i)
            if (dispensers[i].item_dispenser == null)
                dispensers.RemoveAt(i);

        // Remove invaid fixtures
        for (int i = 0; i < found_fixtures.Count; ++i)
            if (found_fixtures[i] != null)
                if (found_fixtures[i].building == null || found_fixtures[i].path == null)
                    found_fixtures[i] = null;

        // Loop over all the elements in the room
        foreach (var l in town_path_element.elements_in_room(path_element.room))
        {
            // Add new dispensers
            var d = l.GetComponentInParent<item_dispenser>();
            if (d != null)
            {
                bool already_found = false;
                foreach (var d2 in dispensers)
                    if (d2.item_dispenser == d)
                    {
                        already_found = true;
                        break;
                    }

                if (!already_found)
                {
                    var p = town_path_element.path.get(path_element, l);
                    if (p != null) dispensers.Add(new dispenser(d, p));
                }
            }

            // Look for fixtures
            var b = l.GetComponentInParent<building_material>();
            if (b == null) continue;
            for (int i = 0; i < found_fixtures.Count; ++i)
            {
                if (found_fixtures[i] != null) continue; // Already found
                if (b.name != required_fixtures[i].name) continue; // Not the right building
                var path = town_path_element.path.get(path_element, l);
                if (path == null) continue; // Not pathable

                // Found matching pathable fixture
                found_fixtures[i] = new fixture(b, path);
                break;
            }
        }

        // Ensure all fixutres have been found
        foreach (var f in found_fixtures) if (f == null) return false;

        return true;
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        validate_fixtures();
        string ret = base.added_inspection_text();
        ret += "\nWorkshop requirements:";
        if (required_fixtures.Count == 0) ret += "\nNone";
        else for (int i = 0; i < required_fixtures.Count; ++i)
                ret += "\n  " + (found_fixtures[i] == null ? "[ ]" : "[x]") + " " + required_fixtures[i].name;
        ret += "\n" + dispensers.Count + " dispensers connected";
        return ret;
    }
}
