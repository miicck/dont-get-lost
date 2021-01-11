using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that deals with setting up the recipe choices
/// when crafting from the inventory <see cref="crafting_input.craft_from"/>
/// to the inventory <see cref="crafting_input.craft_to"/>. </summary>
public class crafting_input : MonoBehaviour
{
    public Transform options_go_here;
    public inventory craft_from;
    public inventory craft_to;
    public string recipes_folder;
    protected recipe[] recipes;

    private void Start()
    {
        craft_from.add_on_change_listener(update_recipies);
    }

    protected virtual void load_recipies()
    {
        if (recipes_folder != null && recipes_folder.Trim().Length != 0)
            recipes = Resources.LoadAll<recipe>(recipes_folder);
        else
            throw new System.Exception("No recipe folder specified for " + name);
    }

    public void update_recipies()
    {
        load_recipies();

        foreach (Transform t in options_go_here)
            Destroy(t.gameObject);

        foreach (var rec in recipes)
            if (rec.can_craft(craft_from))
            {
                var entry = rec.get_entry();
                entry.transform.SetParent(options_go_here);
                entry.button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
                {
                    int to_craft = controls.held((controls.BIND)controls.BIND.CRAFT_FIVE) ? 5 : 1;
                    for (int n = 0; n < to_craft; ++n)
                        rec.craft(craft_from, craft_to);
                }));
            }
    }
}