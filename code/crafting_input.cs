using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crafting_input : MonoBehaviour
{
    public Transform options_go_here;
    public inventory craft_from;
    public inventory craft_to;

    private void Start()
    {
        craft_from.add_on_change_listener(update_recipies);
    }

    void update_recipies()
    {
        foreach (Transform t in options_go_here)
            Destroy(t.gameObject);

        foreach (var rec in recipes())
            if (rec.can_craft(craft_from))
            {
                var entry = rec.get_entry();
                entry.transform.SetParent(options_go_here);
                entry.button.onClick.AddListener(() =>
                {
                    int to_craft = 1;
                    if (Input.GetKey(KeyCode.LeftShift))
                        to_craft = 5;
                    for (int n = 0; n < to_craft; ++n)
                        rec.craft(craft_from, craft_to);
                });
            }
    }

    recipe[] recipes()
    {
        return new recipe[]
        {
            new recipe("planks", 1, new ingredient("log",1)),
            new recipe("stick",5, new ingredient("log",1))
        };
    }
}

class ingredient
{
    string item_name;
    int count;

    public ingredient(string item_name, int count)
    {
        this.item_name = item_name;
        this.count = count;
    }

    public bool in_inventory(inventory i)
    {
        foreach (var s in i.slots)
            if (s.item == item_name && s.count >= count)
                return true;
        return false;
    }

    public void on_craft(inventory i)
    {
        i.remove(item_name, count);
    }

    public string str()
    {
        if (count > 1) return count + " " + item_name;
        return item_name;
    }
}

class recipe
{
    public string product;
    public int product_count;
    public ingredient[] ingredients;

    public recipe(string product, int product_count, params ingredient[] ingredients)
    {
        this.product = product;
        this.product_count = product_count;
        this.ingredients = ingredients;
    }

    public crafting_entry get_entry()
    {
        var ce = crafting_entry.create();
        var itm = item.load_from_name(product);
        ce.image.sprite = itm.sprite;
        ce.text.text = "";
        foreach (var i in ingredients)
            ce.text.text += i.str() + " + ";
        ce.text.text = ce.text.text.Substring(0, ce.text.text.Length - 2);

        if (product_count > 1) ce.text.text += "-> " + product_count + " " + product;
        else ce.text.text += "-> " + product;
        return ce;
    }

    public bool can_craft(inventory i)
    {
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
        to.add(product, product_count);
    }
}