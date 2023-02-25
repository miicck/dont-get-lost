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

    private void Start() => craft_from.add_on_change_listener(update_recipies);
    protected virtual AudioClip crafting_sound() => Resources.Load<AudioClip>("sounds/click_2");
    protected virtual float crafting_sound_volume() => 1f;

    protected virtual void load_recipies()
    {
        if (recipes != null) return; // Already loaded
        if (recipes_folder != null && recipes_folder.Trim().Length != 0)
        {
            recipes = Resources.LoadAll<recipe>(recipes_folder);
            return;
        }
        throw new System.Exception("No recipe folder specified for " + name);
    }

    // The recipes who's buttons have appeared this session =>
    // their location should remain fixed so they don't move 
    // around during crafting.
    HashSet<recipe> saved_recipe_buttons = new HashSet<recipe>();

    // Closed the menu => no longer need to save the recipe buttons
    private void OnDisable() => saved_recipe_buttons.Clear();

    public void clear_greyed_out()
    {
        saved_recipe_buttons.Clear();
        update_recipies();
    }

    void update_recipies()
    {
        load_recipies();

        foreach (Transform t in options_go_here)
            Destroy(t.gameObject);

        foreach (var rec in recipes)
        {
            bool can_craft = rec.can_craft(craft_from);

            if (can_craft || saved_recipe_buttons.Contains(rec))
            {
                var entry = rec.get_entry(options_go_here);
                saved_recipe_buttons.Add(rec);

                if (can_craft) entry.button.onClick.AddListener(() =>
                {
                    player.current.play_sound(crafting_sound(), volume: crafting_sound_volume());
                    int to_craft = controls.held(controls.BIND.CRAFT_FIVE) ? 5 : 1;
                    for (int n = 0; n < to_craft; ++n)
                        rec.craft(craft_from, craft_to);
                });
                else entry.button.colors = new UnityEngine.UI.ColorBlock
                {
                    fadeDuration = 0,
                    normalColor = ui_colors.greyed_out,
                    selectedColor = ui_colors.greyed_out,
                    disabledColor = ui_colors.greyed_out,
                    highlightedColor = ui_colors.greyed_out,
                    pressedColor = ui_colors.greyed_out,
                    colorMultiplier = 1f,
                };
            }
        }
    }
}