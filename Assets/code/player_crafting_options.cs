using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_crafting_options : crafting_input
{
    new public string recipes_folder => "recipes/by_hand";

    public UnityEngine.UI.Text title;

    AudioClip custom_sound = null;
    float custom_sound_vol = 1f;
    protected override AudioClip crafting_sound()
    {
        if (custom_sound == null)
            return base.crafting_sound();
        return custom_sound;
    }

    protected override float crafting_sound_volume()
    {
        if (custom_sound == null)
            return base.crafting_sound_volume();
        return custom_sound_vol;
    }

    protected override void load_recipies()
    {
        // Load both the "by hand" recipes ...
        var loaded = new List<recipe>();
        loaded.AddRange(Resources.LoadAll<recipe>(recipes_folder));

        // ... and the additional recipes of the object we're interacting with
        var to_add = player.current.interactions.additional_recipes(
            out string name, out custom_sound, out custom_sound_vol);

        if (to_add != null)
        {
            loaded = new List<recipe>(to_add);
            title.text = name;
        }
        else title.text = "Crafting";

        recipes = loaded.ToArray();
    }
}
