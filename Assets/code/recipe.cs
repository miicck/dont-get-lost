using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(product))]
[RequireComponent(typeof(ingredient))]
public class recipe : MonoBehaviour
{
    public product[] products { get => GetComponents<product>(); }
    public ingredient[] ingredients { get => GetComponents<ingredient>(); }

    public crafting_entry get_entry()
    {
        var ce = crafting_entry.create();
        ce.image.sprite = products[0].sprite();
        ce.text.text = craft_string();
        return ce;
    }

    public string craft_string()
    {
        string ret = "";
        foreach (var i in ingredients)
            ret += i.str() + " + ";
        ret = ret.Substring(0, ret.Length - 2);
        ret += "> " + product.product_quantities_list(products);
        return ret;
    }

    string recipe_book_string()
    {
        string ret = product.product_quantities_list(products);
        ret += " < ";
        foreach (var i in ingredients)
            ret += i.str() + " + ";
        ret = ret.Substring(0, ret.Length - 2);
        return ret;
    }

    /// <summary> Check if this recipe is craftable from the given item collection.
    /// If so, the dictionary <paramref name="to_use"/> will contain the
    /// ingredients/quantities that can be used from the collection to
    /// fulfil the recipe. If not, then it will contain the maximal partial set of
    /// ingredients if <paramref name="maximal_use"/> is set; otherwise it should be ignored. </summary>
    public bool can_craft(IItemCollection i, out Dictionary<string, int> to_use, bool maximal_use = false)
    {
        to_use = new Dictionary<string, int>();
        bool complete = true;
        foreach (var ing in ingredients)
            if (!ing.find(i, ref to_use))
            {
                complete = false;
                if (!maximal_use) return false;
            }
        return complete;
    }

    /// <summary> Simmilar to <see cref="can_craft(IEnumerable{IItemCollection}, out Dictionary{IItemCollection, Dictionary{string, int}}, bool)"/>
    /// but will return the number of times the recipe can be crafted from the given item collection. </summary>
    public int count_can_craft(IItemCollection i, out Dictionary<string, int> to_use, bool maximal_use = false, int max_count = 10)
    {
        to_use = new Dictionary<string, int>();
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
    public int count_can_craft(IItemCollection i, bool maximal_use = false, int max_count = 10)
    {
        return count_can_craft(i, out Dictionary<string, int> ignored, maximal_use: maximal_use, max_count: max_count);
    }

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

    public bool can_craft(IItemCollection i)
    {
        return can_craft(i, out Dictionary<string, int> ignored);
    }

    public bool craft(IItemCollection from, IItemCollection to, bool track_production = false)
    {
        if (!can_craft(from, out Dictionary<string, int> to_use)) return false;
        foreach (var kv in to_use) from.remove(kv.Key, kv.Value);
        foreach (var p in products) p.create_in(to, track_production: track_production);
        return true;
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

    //##############//
    // STATIC STUFF //
    //##############//

    public static List<KeyValuePair<string, recipe[]>> all_recipies()
    {
        List<KeyValuePair<string, recipe[]>> ret = new List<KeyValuePair<string, recipe[]>>();
        ret.Add(new KeyValuePair<string, recipe[]>("by_hand", Resources.LoadAll<recipe>("recipes/by_hand")));

        foreach (var ac in Resources.LoadAll<auto_crafter>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(ac.name, Resources.LoadAll<recipe>("recipes/workbenches/" + ac.name)));

        foreach (var ac in Resources.LoadAll<auto_crafter>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(ac.name, Resources.LoadAll<recipe>("recipes/autocrafters/" + ac.name)));

        foreach (var ac in Resources.LoadAll<farming_patch>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(ac.name, Resources.LoadAll<recipe>("recipes/farming_spots/" + ac.name)));

        foreach (var w in Resources.LoadAll<workshop>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(w.name + " (made by settlers)", Resources.LoadAll<recipe>("recipes/workshops/" + w.name)));

        foreach (var g in Resources.LoadAll<gradual_processor>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(g.name, Resources.LoadAll<recipe>("recipes/gradual_processors/" + g.name)));

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