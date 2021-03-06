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

    string craft_string()
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

    public bool can_craft(IItemCollection i, out Dictionary<string, int> to_use)
    {
        to_use = new Dictionary<string, int>();
        foreach (var ing in ingredients)
            if (!ing.find(i, ref to_use))
                return false;

        return true;
    }

    public bool can_craft(IItemCollection i)
    {
        return can_craft(i, out Dictionary<string, int> ignored);
    }

    public bool craft(IItemCollection from, IItemCollection to)
    {
        if (!can_craft(from, out Dictionary<string, int> to_use)) return false;
        foreach (var kv in to_use) from.remove(kv.Key, kv.Value);
        foreach (var p in products) p.create_in(to);
        return true;
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

        foreach (var ac in Resources.LoadAll<farming_spot>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(ac.name, Resources.LoadAll<recipe>("recipes/farming_spots/" + ac.name)));

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

        // Add all items sold at shops to the recipe book
        foreach (var s in shop.all_shop_types())
        {
            string entry = "\n" + s.shop_name() + "\n";

            bool found = false;
            foreach (var item_name in s.items_sold())
            {
                var i = Resources.Load<item>("items/" + item_name);
                if (i == null)
                {
                    Debug.LogError("Could not find the item " + item_name);
                    continue;
                }
                string line = i.display_name + " < " + i.value + (i.value > 1 ? " coins" : " coin");
                if (line.Contains(find))
                {
                    found = true;
                    entry += "  " + line + "\n";
                }
            }

            if (found)
                text += entry;
        }

        // Add all items bought by shop
        foreach (var s in shop.all_shop_types())
        {
            string entry = "\n" + s.shop_name() + "\n";

            bool found = false;
            foreach (var item_name in s.items_bought())
            {
                var i = Resources.Load<item>("items/" + item_name);
                if (i == null)
                {
                    Debug.LogError("Could not find the item " + item_name);
                    continue;
                }
                string line = i.value + (i.value > 1 ? " coins" : " coin") + " < " + i.display_name;
                if (line.Contains(find))
                {
                    found = true;
                    entry += "  " + line + "\n";
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
}

public abstract class ingredient : MonoBehaviour
{
    public abstract string str();
    public abstract string satisfaction_string(IItemCollection i, ref Dictionary<string, int> in_use);
    public abstract bool find(IItemCollection i, ref Dictionary<string, int> in_use);
    public abstract float average_value();
}