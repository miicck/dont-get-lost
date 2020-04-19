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

        var planks = new recipe("planks", new ingredient("log", 1));
        if (planks.can_craft(craft_from))
        {
            var entry = planks.get_entry();
            entry.transform.SetParent(options_go_here);
            entry.button.onClick.AddListener(() =>
            {
                planks.craft(craft_from, craft_to);
            });
        }
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
        return item_name;
    }
}

class recipe
{
    public string product;
    public ingredient[] ingredients;

    public recipe(string product, params ingredient[] ingredients)
    {
        this.product = product;
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
        ce.text.text += "-> " + product;
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
        foreach (var ing in ingredients)
            ing.on_craft(from);
        to.add(product, 1);
    }
}