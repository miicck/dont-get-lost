using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class auto_crafter : building_material, IPlayerInteractable
{
    static auto_crafter()
    {
        tips.add("The text at the top of the crafting menu will change when " +
            "interacting with objects that allow crafting different recipes.");
    }

    public float craft_time = 1f;
    public string recipes_folder;
    public List<GameObject> enable_when_crafting = new List<GameObject>();

    recipe[] recipies;

    simple_item_collection pending_inputs = new simple_item_collection();
    simple_item_collection pending_outputs = new simple_item_collection();

    item_input[] inputs => GetComponentsInChildren<item_input>();
    item_output[] outputs => GetComponentsInChildren<item_output>();

    /// <summary> The recipe that is currently being crafted. </summary>
    recipe currently_crafting
    {
        get => _currently_crafting;
        set
        {
            _currently_crafting = value;

            if (value != null) Invoke("complete_crafting", craft_time);

            foreach (var ec in enable_when_crafting)
                ec.SetActive(value != null);
        }
    }
    recipe _currently_crafting;

    void Start()
    {
        // Don't start the crafting updates if this isn't the placed version
        if (is_blueprint || is_equpped)
            return;

        // Load the recipes
        recipies = Resources.LoadAll<recipe>(recipes_folder);

        // Setup input listeners
        foreach (var ip in inputs)
            ip.add_on_change_listener(() =>
            {
                // Put all inputs intp pending inputs
                foreach (var itm in ip.relesae_all_items())
                {
                    pending_inputs.add(itm, 1);
                    Destroy(itm.gameObject);
                }

                // Chosen recipe is out of range
                if (chosen_recipe.value < 0 ||
                    chosen_recipe.value >= recipies.Length)
                    return;

                // See if we can craft the chosen recipe
                var rec = recipies[chosen_recipe.value];
                if (rec.can_craft(pending_inputs))
                    currently_crafting = rec;
            });

        // Initially, not crafting anything
        currently_crafting = null;
    }

    void complete_crafting()
    {
        if (currently_crafting == null)
            return;

        if (currently_crafting.craft(pending_inputs, pending_outputs))
        {
            // Crafting success
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

        // If we can immedately craft again, do so, otherwise
        // set the currently crafting recipe to null
        if (currently_crafting.can_craft(pending_inputs))
            Invoke("complete_crafting", craft_time);
        else
            currently_crafting = null;
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

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public override player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = base.player_interactions().prepend(
            new menu(this),
            new player_inspectable(transform)
            {
                text = () =>
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

                    if (currently_crafting != null)
                    {
                        info += "Currently crafting " +
                            product.product_plurals_list(currently_crafting.products) + "\n";
                    }

                    return info;
                }
            });
        return interactions;
    }

    class menu : left_player_menu
    {
        auto_crafter crafter;
        public menu(auto_crafter crafter) : base(crafter.display_name) { this.crafter = crafter; }

        public override recipe[] additional_recipes(out string name)
        {
            name = crafter.display_name;
            return crafter.recipies;
        }

        protected override RectTransform create_menu()
        {
            if (crafter.outputs.Length == 0 || crafter.inputs.Length == 0)
                return null;

            var recipe_buttons = new crafting_entry[crafter.recipies.Length];

            var left_menu = Resources.Load<RectTransform>("ui/autocrafter").inst();
            var content = left_menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

            for (int i = 0; i < crafter.recipies.Length; ++i)
            {
                // Create the recipe selection button
                recipe_buttons[i] = crafter.recipies[i].get_entry();
                var button_i = recipe_buttons[i];
                button_i.transform.SetParent(content);

                // Copies for lambda function
                int i_copy = i;
                var reset_colors = button_i.button.colors;

                button_i.button.onClick.AddListener((() =>
                {
                    if (controls.held(controls.BIND.QUICK_ITEM_TRANSFER))
                    {
                        // Transfer the recipe ingredients to the crafting menu
                        if (player.current == null) return;

                        var r = crafter.recipies[i_copy];
                        bool can_craft = r.can_craft(player.current.inventory, out Dictionary<string, int> to_use);
                        foreach (var kv in to_use)
                        {
                            if (player.current.inventory.remove(kv.Key, kv.Value))
                                player.current.crafting_menu.add(kv.Key, kv.Value);
                        }
                        return;
                    }

                    crafter.chosen_recipe.value = i_copy;

                    // Update colors to highlight selection
                    for (int j = 0; j < crafter.recipies.Length; ++j)
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
                }));
            }

            // Simulate a click on the initially-selected button
            if (crafter.chosen_recipe.value >= 0 && crafter.chosen_recipe.value < recipe_buttons.Length)
                recipe_buttons[crafter.chosen_recipe.value].button.onClick.Invoke();

            return left_menu;
        }
    }
}
