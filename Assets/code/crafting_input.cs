using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crafting_input : MonoBehaviour
{
    public Transform options_go_here;
    public inventory craft_from;
    public inventory craft_to;
    public string default_recipes_folder;
    recipe[] recipes;

    private void Start()
    {
        if (default_recipes_folder != null && default_recipes_folder.Trim().Length != 0)
            load_recipies(default_recipes_folder);

        craft_from.add_on_change_listener(update_recipies);
    }

    public void load_recipies(string load_folder)
    {
        recipes = Resources.LoadAll<recipe>(load_folder);
    }

    void update_recipies()
    {
        foreach (Transform t in options_go_here)
            Destroy(t.gameObject);

        foreach (var rec in recipes)
            if (rec.can_craft(craft_from))
            {
                var entry = rec.get_entry();
                entry.transform.SetParent(options_go_here);
                entry.button.onClick.AddListener(() =>
                {
                    int to_craft = controls.key_down(controls.BIND.CRAFT_FIVE) ? 5 : 1;
                    for (int n = 0; n < to_craft; ++n)
                        rec.craft(craft_from, craft_to);
                });
            }
    }
}