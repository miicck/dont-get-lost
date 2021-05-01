using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workshop : settler_interactable_options, IAddsToInspectionText
{
    new public town_path_element path_element;
    public List<building_material> required_fixtures;

    bool has_access_to(building_material b)
    {
        foreach (var l in town_path_element.elements_in_room(path_element.room))
        {
            var b2 = l.GetComponentInParent<building_material>();
            if (b2 == null) continue;
            if (b2.name == b.name) return true;
        }
        return false;
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

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        string ret = base.added_inspection_text();
        ret += "\nWorkshop requirements:";
        foreach (var f in required_fixtures)
            ret += "\n  " + (has_access_to(f) ? "[x]" : "[ ]") + " " + f.display_name;
        return ret;
    }
}
