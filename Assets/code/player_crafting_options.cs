using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_crafting_options : crafting_input
{
    new public string recipes_folder => "recipes/by_hand";

    public UnityEngine.UI.Text title;

    protected override void load_recipies()
    {
        // Load both the "by hand" recipes ...
        var loaded = new List<recipe>();
        loaded.AddRange(Resources.LoadAll<recipe>(recipes_folder));

        // ... and the additional recipes of the object we're interacting with
        string name = null;
        var to_add = player.current.interactions?.additional_recipes(out name);
        if (to_add != null)
        {
            loaded = new List<recipe>(to_add);
            title.text = name;
        }
        else title.text = "Crafting";

        recipes = loaded.ToArray();
    }
}
