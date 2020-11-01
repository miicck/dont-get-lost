using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_crafting_options : crafting_input
{
    new public string recipes_folder => "recipes/by_hand";

    public override void load_recipies()
    {
        // Load both the "by hand" recipes ...
        var loaded = new List<recipe>();
        loaded.AddRange(Resources.LoadAll<recipe>(recipes_folder));

        // ... and the additional recipes of the object we're interacting with
        var to_add = player.current.left_menu?.additional_recipes();
        if (to_add != null) loaded.AddRange(to_add);

        recipes = loaded.ToArray();
    }
}
