using UnityEngine;

public static class community_documentation
{
    public static void generate()
    {
        Debug.Log("Generating community documentation...");

        help_book.add_entry("towns/Crafting Stations",
            "There are 3 types of Crafting stations." 
        );

        help_book.add_entry("towns/Crafting Stations/Automated Crafting",
            "For automated crafting stations, you select the recipe you want "+
            "it to craft from the menu on the left. If the required ressources are coming into "+
            "the inputs of the station, then it will craft the recipe automatically." 
        );

        help_book.add_entry("towns/Crafting Stations/Manual Crafting",
            "For manual crafting stations, you put the ressources you will "+
            "use to craft with in the crafting slots on the right of "+
            "your inventory, and all the available recipes will show up." 
        );

        help_book.add_entry("towns/Crafting Stations/Settler Crafting",
            "For crafting stations requiring a settler, you select the "+
            "recipe you want to craft. You must have a material cupboard "+
            "with the materials you require for the selected recipe. "+
            "Once everything required is there, a settler will start "+
            "crafting your recipe at the crating station."
        );
    }
}
