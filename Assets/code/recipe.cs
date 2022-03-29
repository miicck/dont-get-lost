using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(product))]
[RequireComponent(typeof(ingredient))]
public class recipe : MonoBehaviour, IRecipeInfo
{
    public product[] products => GetComponents<product>();
    public ingredient[] ingredients => GetComponents<ingredient>();

    public crafting_entry get_entry()
    {
        var ce = crafting_entry.create();
        ce.image.sprite = products[0].sprite;
        ce.text.text = craft_string();
        return ce;
    }

    public string craft_string()
    {
        string ret = "";
        foreach (var i in ingredients)
            ret += i.str() + " + ";
        ret = ret.Substring(0, ret.Length - 2);
        ret += "> " + item_product.product_quantities_list(products);
        return ret;
    }

    public string recipe_book_string()
    {
        string ret = item_product.product_quantities_list(products);
        ret += " < ";
        foreach (var i in ingredients)
            ret += i.str() + " + ";
        ret = ret.Substring(0, ret.Length - 2);
        return ret;
    }

    public float time_requirement()
    {
        float t = 0;
        foreach (var tr in GetComponents<time_requirement>())
            t += tr.time;
        return t;
    }

    public float average_amount_produced(item i)
    {
        float ret = 0;
        foreach (var p in products)
            ret += p.average_amount_produced(i);
        return ret;
    }

    public float average_ingredients_value()
    {
        float ret = 0;
        foreach (var i in ingredients)
            ret += i.average_value();
        return ret;
    }

    //##################//
    // CAN CRAFT CHECKS //
    //##################//

    public bool unlocked
    {
        get
        {
            foreach (var p in products)
                if (!p.unlocked)
                    return false;
            return technology_requirement.unlocked(this);
        }
    }

    /// <summary> Check if this recipe is craftable from the given item collection.
    /// If so, the dictionary <paramref name="to_use"/> will contain the
    /// ingredients/quantities that can be used from the collection to
    /// fulfil the recipe. If not, then it will contain the maximal partial set of
    /// ingredients if <paramref name="maximal_use"/> is set; otherwise it should be ignored. </summary>
    public bool can_craft(IItemCollection i, out Dictionary<string, int> to_use, bool maximal_use = false)
    {
        to_use = new Dictionary<string, int>();

        if (!unlocked)
            return false;

        bool complete = true;
        foreach (var ing in ingredients)
            if (!ing.find(i, ref to_use))
            {
                complete = false;
                if (!maximal_use) return false;
            }

        return complete;
    }

    public bool can_craft(IItemCollection i) => can_craft(i, out Dictionary<string, int> ignored);

    /// <summary> Simmilar to <see cref="can_craft(IEnumerable{IItemCollection}, out Dictionary{IItemCollection, Dictionary{string, int}}, bool)"/>
    /// but will return the number of times the recipe can be crafted from the given item collection. </summary>
    public int count_can_craft(IItemCollection i, out Dictionary<string, int> to_use, bool maximal_use = false, int max_count = 10)
    {
        to_use = new Dictionary<string, int>();

        if (!unlocked)
            return 0;

        int craft_count = 0;
        bool search_again = true;

        while (search_again)
        {
            foreach (var ing in ingredients)
                if (!ing.find(i, ref to_use))
                {
                    if (!maximal_use) return craft_count;
                    search_again = false;
                }

            if (search_again && ++craft_count >= max_count)
                return max_count;
        }

        return craft_count;
    }

    /// <summary> Overload of <see cref="count_can_craft(IItemCollection, out Dictionary{string, int}, bool)"/>, 
    /// without to-use dictionary. </summary>
    public int count_can_craft(IItemCollection i, bool maximal_use = false, int max_count = 10) =>
        count_can_craft(i, out Dictionary<string, int> ignored, maximal_use: maximal_use, max_count: max_count);

    /// <summary> Check if this recipe is craftable from the given item collections.
    /// If so, the dictionary <paramref name="to_use"/> will contain the
    /// ingredients/quantities that can be used from each collection to
    /// fulfil the recipe. Note: this only distributes distinct ingredients
    /// across multiple collections; indivudual ingredients must be satisfied
    /// by an individual collection. </summary>
    public bool can_craft(IEnumerable<IItemCollection> ics,
        out Dictionary<IItemCollection, Dictionary<string, int>> to_use, bool maximal_use = false)
    {
        // Initialise the dictionary-of-dictionaries
        to_use = new Dictionary<IItemCollection, Dictionary<string, int>>();
        foreach (var i in ics) to_use[i] = new Dictionary<string, int>();

        if (!unlocked)
            return false;

        // Check for each ingredient
        bool complete = true;
        foreach (var ing in ingredients)
        {
            // Search each collection for this ingredient
            bool found = false;
            foreach (var i in ics)
            {
                var tu = to_use[i];
                if (ing.find(i, ref tu))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                complete = false;
                if (!maximal_use) return false;
            }
        }
        return complete;
    }

    public bool craft(IItemCollection from, IItemCollection to, bool track_production = false)
    {
        if (!can_craft(from, out Dictionary<string, int> to_use)) return false;
        foreach (var kv in to_use) from.remove(kv.Key, kv.Value);
        foreach (var p in products) p.create_in(to, track_production: track_production);
        return true;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static List<KeyValuePair<string, IRecipeInfo[]>> all_recipies()
    {
        List<KeyValuePair<string, IRecipeInfo[]>> ret = new List<KeyValuePair<string, IRecipeInfo[]>>();
        ret.Add(new KeyValuePair<string, IRecipeInfo[]>("by_hand", Resources.LoadAll<recipe>("recipes/by_hand")));

        foreach (var ac in Resources.LoadAll<auto_crafter>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(ac.name, Resources.LoadAll<recipe>("recipes/workbenches/" + ac.name)));

        foreach (var ac in Resources.LoadAll<auto_crafter>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(ac.name, Resources.LoadAll<recipe>("recipes/autocrafters/" + ac.name)));

        foreach (var ac in Resources.LoadAll<farming_patch>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(ac.name, Resources.LoadAll<recipe>("recipes/farming_spots/" + ac.name)));

        foreach (var w in Resources.LoadAll<workshop>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(w.name + " (operated by settlers)", Resources.LoadAll<recipe>("recipes/workshops/" + w.name)));

        foreach (var g in Resources.LoadAll<gradual_processor>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(g.name, Resources.LoadAll<recipe>("recipes/gradual_processors/" + g.name)));

        foreach (var ls in Resources.LoadAll<livestock_shelter>("items"))
            ret.Add(new KeyValuePair<string, IRecipeInfo[]>(ls.name, new IRecipeInfo[] { ls }));

        foreach (var c in Resources.LoadAll<character>("characters"))
        {
            var r = c.looting_products_recipe();
            if (r != null)
                ret.Add(new KeyValuePair<string, IRecipeInfo[]>(c.name, new IRecipeInfo[] { r }));
        }

        return ret;
    }

    static string recipe_book_text(string find)
    {
        string text = "Recipes\n";

        // Add all recipes to the recipe book
        foreach (var kv in all_recipies())
        {
            string entry = "";
            bool found = false;

            entry += "\n" + kv.Key.Replace('_', ' ').capitalize() + "\n";
            foreach (var r in kv.Value)
            {
                string line = r.recipe_book_string();
                if (line.Contains(find))
                {
                    entry += "  " + line + "\n";
                    found = true;
                }
            }

            if (found)
                text += entry;
        }

        return text;
    }

    public static RectTransform recipe_book
    {
        get
        {
            if (_recipe_book == null)
            {
                // Create the recipe book
                _recipe_book = Resources.Load<RectTransform>("ui/recipe_book").inst();
                var text = _recipe_book.GetComponentInChildren<UnityEngine.UI.Text>();

                _recipe_book.transform.SetParent(game.canvas.transform);
                _recipe_book.anchoredPosition = Vector2.zero; // Middle of screen
                _recipe_book.gameObject.SetActive(false); // Recipe book starts closed

                // Setup the find function
                var find = _recipe_book.GetComponentInChildren<UnityEngine.UI.InputField>();
                find.text = recipe_book_text("");
                find.onValueChanged.AddListener((val) =>
                {
                    text.text = recipe_book_text(val);
                });
            }

            // Reset the search
            var input = _recipe_book.GetComponentInChildren<UnityEngine.UI.InputField>();
            input.text = "";

            return _recipe_book;
        }
    }
    static RectTransform _recipe_book;

    public class checklist
    {
        public recipe recipe { get; private set; }
        public simple_item_collection stored { get; private set; }

        public checklist(recipe recipe)
        {
            stored = new simple_item_collection();
            this.recipe = recipe;
        }

        public enum CHECK_OFF_RESULT
        {
            ALREADY_COMPLETE,
            NEVER_NEEDED,
            NOT_NEEDED_RIGHT_NOW,
            ADDED,
            ADDED_AND_COMPLETED
        }

        public CHECK_OFF_RESULT try_check_off(item i)
        {
            // Check if this item can't be used for the recipe
            simple_item_collection needed_check = new simple_item_collection();
            needed_check.add(i, 1);
            recipe.can_craft(needed_check, out Dictionary<string, int> to_use, maximal_use: true);
            if (to_use.Count == 0) return CHECK_OFF_RESULT.NEVER_NEEDED;

            if (recipe.can_craft(stored, out Dictionary<string, int> in_use_before, maximal_use: true))
                return CHECK_OFF_RESULT.ALREADY_COMPLETE; // I don't need any more items    

            stored.add(i, 1);
            if (recipe.can_craft(stored, out Dictionary<string, int> in_use_after, maximal_use: true))
                return CHECK_OFF_RESULT.ADDED_AND_COMPLETED; // I completed the recipe

            if (in_use_before.TryGetValue(i.name, out int i_used_before))
            {
                if (!in_use_after.TryGetValue(i.name, out int i_used_after))
                    throw new System.Exception("This should not be possible");

                if (i_used_after > i_used_before)
                    return CHECK_OFF_RESULT.ADDED; // I added to the useful item total
            }
            else if (in_use_after.ContainsKey(i.name))
                return CHECK_OFF_RESULT.ADDED; // I am a new ingredient that was useful for the recipe

            // I was not useful, remove me from the collection
            stored.remove(i, 1);
            return CHECK_OFF_RESULT.NOT_NEEDED_RIGHT_NOW;
        }

        public bool craft_to(IItemCollection col, bool track_production = false)
        {
            return recipe.craft(stored, col, track_production: track_production);
        }

        public void clear()
        {
            stored = new simple_item_collection();
        }
    }
}

public abstract class ingredient : MonoBehaviour
{
    public abstract string str();
    public abstract string satisfaction_string(IItemCollection i, ref Dictionary<string, int> in_use);
    public abstract bool find(IItemCollection i, ref Dictionary<string, int> in_use);
    public abstract float average_value();
}

public abstract class product : MonoBehaviour
{
    public abstract bool unlocked { get; }
    public abstract Sprite sprite { get; }
    public abstract float average_amount_produced(item i);
    public abstract string product_name();
    public abstract string product_name_plural();
    public abstract string product_name_quantity();
    public abstract void create_in(IItemCollection inv, int count = 1, bool track_production = false);
    public abstract void create_in_node(item_node node, bool track_production = false);

    //##############//
    // STATIC STUFF //
    //##############//

    /// <summary> Convert a list of products to a string describing that list. </summary>
    public static string product_quantities_list(IList<product> products)
    {
        string ret = "";
        for (int i = 0; i < products.Count - 1; ++i)
            ret += products[i].product_name_quantity() + ", ";

        if (products.Count > 1)
        {
            ret = ret.Substring(0, ret.Length - 2);
            ret += " and " + products[products.Count - 1].product_name_quantity();
        }
        else ret = products[0].product_name_quantity();

        return ret;
    }

    public static string product_plurals_list(IList<product> products)
    {
        string ret = "";
        for (int i = 0; i < products.Count - 1; ++i)
            ret += products[i].product_name_plural() + ", ";

        if (products.Count > 1)
        {
            ret = ret.Substring(0, ret.Length - 2);
            ret += " and " + products[products.Count - 1].product_name_plural();
        }
        else ret = products[0].product_name_plural();

        return ret;
    }
}

public interface IRecipeInfo
{
    public string recipe_book_string();
    public float average_amount_produced(item i);
    public float average_ingredients_value();
}

public abstract class product_list_recipe_info : IRecipeInfo
{
    protected IEnumerable<product> products;

    public product_list_recipe_info(IEnumerable<product> products) => this.products = products;
    public abstract string recipe_book_string();
    public abstract float average_ingredients_value();

    public float average_amount_produced(item i)
    {
        float total = 0;
        foreach (var p in products)
            total += p.average_amount_produced(i);
        return total;
    }
}