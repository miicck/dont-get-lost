using UnityEngine;

public static class community_documentation
{
    public static void generate()
    {
        Debug.Log("Generating community documentation...");

        help_book.add_entry("towns/Crafting Stations",
            () => "There are 3 types of Crafting stations."
        );

        help_book.add_entry("towns/Crafting Stations/Automated Crafting",
            () => "For automated crafting stations, you select the recipe you want " +
            "it to craft from the menu on the left. If the required resources are coming into " +
            "the inputs of the station, then it will craft the recipe automatically."
        );
        help_book.add_entry("towns/Crafting Stations/Automated Crafting/Sawmill",
           () => "The sawmill lets you craft recipes automatically, as long as it has " +
            "the resources fed into the top input. You select the automatic recipe " +
            "on the left side when interacting with the sawmill. The output is on " +
            "the side, which you can output in a chest or use gutters to move it elsewhere."
        );
        help_book.add_entry("towns/Crafting Stations/Automated Crafting/Furnace",
            () => "The furnace lets you craft recipes automatically, as long as it has " +
            "the resources in its inputs. The top input is reserved for fuel " +
            "(coal,wood), and on the side,facing up, you will have the resource input." +
            "The output, facing down, will be on the opposite side, which you can output " +
            "in a chest or use gutters to move it elsewhere."
        );


        help_book.add_entry("towns/Crafting Stations/Manual Crafting",
           () => "For manual crafting stations, you put the resources you will " +
            "use to craft with in the crafting slots on the right of " +
            "your inventory, and all the available recipes will show up."
        );

        help_book.add_entry("towns/Crafting Stations/Manual Crafting/Sawmill",
            () => "The sawmill lets you craft recipes manually, as long as you put " +
            "the resources in the right side under the crafting menu. " +
            "You can find the available recipes by pressing <b>" + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) +
            "</b> and looking at the sawmill subsection."
        );

        help_book.add_entry("towns/Crafting Stations/Manual Crafting/Furnace",
            () => "The furnace lets you craft recipes manually, as long as you put " +
            "the resources in the right side under the crafting menu. " +
            "Recipes will require a source of fuel (coal,wood)." +
            "You can find the available recipes by pressing <b>" + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) +
            "</b> and looking at the furnace subsection."
        );
        help_book.add_entry("towns/Crafting Stations/Manual Crafting/Stonemason Table",
            () => "The stonemason table lets you craft recipes manually, as long as you put " +
            "the resources in the right side under the crafting menu. " +
            "You can find the available recipes by pressing <b>" + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) +
            "</b> and looking at the stonemason table subsection."
        );


        help_book.add_entry("towns/Crafting Stations/Settler Crafting",
            () => "For crafting stations requiring a settler, you select the " +
            "recipe you want to craft. You must have a material cupboard " +
            "with the materials you require for the selected recipe. " +
            "Once everything required is there, a settler will start " +
            "crafting your recipe at the crating station."
        );
    }
}
