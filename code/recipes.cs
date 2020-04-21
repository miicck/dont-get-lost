using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class recipes
{
    public enum RECIPE_GROUP
    {
        HAND,
        FURNACE,
    }

    static Dictionary<RECIPE_GROUP, recipe[]> recipe_groups =
        new Dictionary<RECIPE_GROUP, recipe[]>();

    public static recipe[] recipies(RECIPE_GROUP group)
    {
        recipe[] ret;
        if (!recipe_groups.TryGetValue(group, out ret))
        {
            ret = generate_group(group);
            recipe_groups[group] = ret;
        }
        return ret;
    }

    static recipe[] generate_group(RECIPE_GROUP group)
    {
        switch (group)
        {
            case RECIPE_GROUP.HAND:
                return new recipe[]
                {
                    new recipe("planks", 1, new ingredient.item("log", 1)),
                    new recipe("stick",  5, new ingredient.item("log", 1))
                };

            case RECIPE_GROUP.FURNACE:
                return new recipe[]
                {
                    new recipe("iron", 1, new ingredient.item("log", 1)),
                };

            default:
                Debug.LogError("No recipes for recipe group: " + group);
                return new recipe[0];
        }
    }
}
