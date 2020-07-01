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
        ret += "> " + product.product_list(products);
        return ret;
    }

    public bool can_craft(inventory i)
    {
        if (ingredients.Length == 0)
            throw new System.Exception("Recipies should have > 0 ingredients!");

        foreach (var ing in ingredients)
            if (!ing.in_inventory(i))
                return false;
        return true;
    }

    public void craft(inventory from, inventory to)
    {
        if (!can_craft(from)) return;
        foreach (var ing in ingredients)
            ing.on_craft(from);
        foreach (var p in products)
            p.create_in_inventory(to);
    }

    static List<KeyValuePair<string, recipe[]>> all_recipies()
    {
        List<KeyValuePair<string, recipe[]>> ret = new List<KeyValuePair<string, recipe[]>>();
        ret.Add(new KeyValuePair<string, recipe[]>("by_hand", Resources.LoadAll<recipe>("recipes/by_hand")));

        foreach (var wb in Resources.LoadAll<workbench>("items"))
            ret.Add(new KeyValuePair<string, recipe[]>(wb.name, Resources.LoadAll<recipe>("recipes/workbenches/" + wb.name)));

        return ret;
    }

    public static RectTransform recipe_book
    {
        get
        {
            if (_recipe_book == null)
            {
                string text = "Recipes\n";

                foreach (var kv in all_recipies())
                {
                    text += "\n" + kv.Key + "\n";
                    foreach (var r in kv.Value)
                        text += "  " + r.craft_string() + "\n";
                }

                _recipe_book = Resources.Load<RectTransform>("ui/recipe_book").inst();
                _recipe_book.GetComponentInChildren<UnityEngine.UI.Text>().text = text;
                _recipe_book.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                _recipe_book.anchoredPosition = Vector2.zero; // Middle of screen
                _recipe_book.gameObject.SetActive(false); // Recipe book starts closed
            }
            return _recipe_book;
        }
    }
    static RectTransform _recipe_book;
}

public abstract class ingredient : MonoBehaviour
{
    public abstract string str();
    public abstract bool in_inventory(inventory i);
    public abstract void on_craft(inventory i);
}