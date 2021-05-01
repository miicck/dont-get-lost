using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workshop : settler_interactable_options, IAddsToInspectionText
{
    new public town_path_element path_element;
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

        public bool valid => building != null && path != null;
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

    recipe current_recipe => recipes[selected_option];

    protected override int options_count => recipes.Length;
    protected override string options_title => "Forging";

    protected override option get_option(int i)
    {
        return new option
        {
            text = recipes[i].craft_string(),
            sprite = recipes[i].products[0].sprite()
        };
    }

    protected override bool ready_to_assign(settler s)
    {
        if (!validate_fixtures()) return false;
        if (!validate_recipe()) return false;
        return true;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        return STAGE_RESULT.TASK_FAILED;
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
                if (found_fixtures[i]?.valid == true) continue; // Already found
                if (b.name != required_fixtures[i].name) continue; // Not the right building
                var path = town_path_element.path.get(path_element, l);
                if (path == null) continue; // Not pathable

                // Found matching pathable fixture
                found_fixtures[i] = new fixture(b, path);
                break;
            }
        }

        // Ensure all fixutres have been found
        foreach (var f in found_fixtures) if (f.building == null) return false;

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
        for (int i = 0; i < required_fixtures.Count; ++i)
            ret += "\n  " + (found_fixtures[i].building == null ? "[ ]" : "[x]") + " " + required_fixtures[i].name;
        ret += "\n" + dispensers.Count + " dispensers connected";
        return ret;
    }
}
