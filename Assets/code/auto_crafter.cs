using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : building_material, IInspectable, ILeftPlayerMenu
{
    public float craft_time = 1f;
    public string recipes_folder;
    recipe[] recipies;

    simple_item_collection pending_inputs = new simple_item_collection();
    simple_item_collection pending_outputs = new simple_item_collection();

    item_input[] inputs => GetComponentsInChildren<item_input>();
    item_output[] outputs => GetComponentsInChildren<item_output>();


    void Start()
    {
        // Don't start the crafting updates if this isn't the placed version
        if (is_blueprint || is_equpped)
            return;

        // Load the recipes
        recipies = Resources.LoadAll<recipe>(recipes_folder);
        InvokeRepeating("crafting_update", craft_time, craft_time);
    }

    void crafting_update()
    {
        // Add inputs to the pending inputs collection
        bool inputs_changed = false;
        foreach (var ip in inputs)
            foreach (var itm in ip.relesae_all_items())
            {
                pending_inputs.add(itm, 1);
                Destroy(itm.gameObject);
                inputs_changed = true;
            }

        // Attempt to craft something
        if (inputs_changed || inputs.Length == 0)
            if (chosen_recipe.value >= 0 && chosen_recipe.value < recipies.Length)
                if (recipies[chosen_recipe.value].craft(pending_inputs, pending_outputs))
                    pending_inputs.clear();

        int output_number = -1;
        while (true)
        {
            // Nothing to output to
            if (outputs.Length == 0) break;

            // Get the next item to output
            var itm = pending_outputs.remove_first();
            if (itm == null) break; // No items left

            // Cycle items to sequential outputs
            output_number = (output_number + 1) % outputs.Length;
            var op = outputs[output_number];

            // Create the item in the output
            op.add_item(create(itm.name,
                        op.transform.position,
                        op.transform.rotation,
                        logistics_version: true));
        }
    }

    //############//
    // NETWORKING //
    //############//

    // Save the chosen recipe
    networked_variables.net_int chosen_recipe;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        chosen_recipe = new networked_variables.net_int();
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    crafting_entry[] recipe_buttons;

    public string left_menu_display_name() { return display_name; }

    RectTransform left_menu;
    public RectTransform left_menu_transform()
    {
        if (left_menu == null)
        {
            recipe_buttons = new crafting_entry[recipies.Length];

            left_menu = Resources.Load<RectTransform>("ui/autocrafter").inst();
            var content = left_menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

            for (int i = 0; i < recipies.Length; ++i)
            {
                // Create the recipe selection button
                recipe_buttons[i] = recipies[i].get_entry();
                var button_i = recipe_buttons[i];
                button_i.transform.SetParent(content);

                // Copies for lambda function
                int i_copy = i;
                var reset_colors = button_i.button.colors;

                button_i.button.onClick.AddListener(() =>
                {
                    chosen_recipe.value = i_copy;

                    // Update colors to highlight selection
                    for (int j = 0; j < recipies.Length; ++j)
                    {
                        var button_j = recipe_buttons[j];
                        var colors = button_j.button.colors;
                        if (j == i_copy)
                        {
                            colors.normalColor = Color.green;
                            colors.pressedColor = Color.green;
                            colors.highlightedColor = Color.green;
                            colors.selectedColor = Color.green;
                            colors.disabledColor = Color.green;
                        }
                        else colors = reset_colors;
                        button_j.button.colors = colors;
                    }
                });
            }

            // Simulate a click on the initially-selected button
            if (chosen_recipe.value >= 0 && chosen_recipe.value < recipe_buttons.Length)
                recipe_buttons[chosen_recipe.value].button.onClick.Invoke();
        }

        return left_menu;
    }

    public inventory editable_inventory() { return null; }
    public void on_left_menu_open() { }
    public void on_left_menu_close() { }
    public recipe[] additional_recipes() { return recipies; }

    //##############//
    // IINspectable //
    //##############//

    public override string inspect_info()
    {
        string info = display_name + "\n";

        var pi = pending_inputs.contents();
        var po = pending_outputs.contents();

        if (pi.Count > 0)
        {
            info += "Pending inputs:\n";
            foreach (var kv in pi)
                info += "    " + kv.Value + " " +
                    kv.Key.singular_or_plural(kv.Value) + "\n";
        }

        if (po.Count > 0)
        {
            info += "Pending outputs:\n";
            foreach (var kv in po)
                info += "    " + kv.Value + " " +
                    kv.Key.singular_or_plural(kv.Value) + "\n";
        }

        return info;
    }
}
