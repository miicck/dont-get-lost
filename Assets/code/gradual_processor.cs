using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gradual_processor : MonoBehaviour, IAddsToInspectionText,
    INonBlueprintable, INonEquipable, INonLogistical
{
    public chest to_process;
    public item_output output;

    recipe crafting;
    int count_crafting = 0;
    float craft_timer = 0;

    float output_timer = 0;
    simple_item_collection pending_output = new simple_item_collection();

    private void Start()
    {
        to_process.add_on_set_inventory_listener(() =>
        {
            to_process.inventory.add_on_change_listener(() =>
            {
                string recipes_folder = "recipes/gradual_processors/" + name;

                // Load potential recipes
                var recipes = Resources.LoadAll<recipe>(recipes_folder);
                if (recipes.Length == 0)
                {
                    Debug.LogError("No recipes found for " + name + " in " + recipes_folder);
                    return;
                }

                // Update the recipe that we are crafting
                // to the craftable recipe that has the most ingredients
                crafting = null;
                count_crafting = 0;
                int max_ingredients = 0;

                foreach (var r in recipes)
                {
                    int can_craft = r.count_can_craft(to_process.inventory, max_count: 100);

                    if (can_craft > 0 && r.ingredients.Length > max_ingredients)
                    {
                        crafting = r;
                        count_crafting = can_craft;
                        max_ingredients = r.ingredients.Length;
                    }
                }
            });

            to_process.inventory.invoke_on_change();
        });
    }


    private void Update()
    {
        if (crafting != null)
        {
            // Run crafting
            craft_timer += Time.deltaTime;
            if (craft_timer > crafting.time_requirement() / count_crafting)
            {
                crafting.craft(to_process.inventory, pending_output, track_production: true);
                craft_timer = 0;
            }
        }

        // Process outputs
        output_timer += Time.deltaTime;
        if (output_timer > 1)
        {
            var itm = pending_output.remove_first();
            if (itm != null) output.add(itm, 1);
            output_timer = 0;
        }
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (crafting == null) return "Not processing";
        return crafting.craft_string() + " [x" + count_crafting + "]";
    }
}
