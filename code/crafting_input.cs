using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crafting_input : MonoBehaviour
{
    public Transform options_go_here;
    public inventory_section craft_from;
    public inventory_section craft_to;
    public recipes.RECIPE_GROUP recipe_group;

    private void Start()
    {
        craft_from.add_on_change_listener(update_recipies);
    }

    void update_recipies()
    {
        foreach (Transform t in options_go_here)
            Destroy(t.gameObject);

        foreach (var rec in recipes.recipies(recipe_group))
            if (rec.can_craft(craft_from))
            {
                var entry = rec.get_entry();
                entry.transform.SetParent(options_go_here);
                entry.button.onClick.AddListener(() =>
                {
                    int to_craft = Input.GetKey(KeyCode.LeftShift) ? 5 : 1;
                    for (int n = 0; n < to_craft; ++n)
                        rec.craft(craft_from, craft_to);
                });
            }
    }
}

public abstract class ingredient
{
    public abstract string str();
    public abstract bool in_inventory(inventory_section i);
    public abstract void on_craft(inventory_section i);

    public class item : ingredient
    {
        string item_name;
        int count;

        public item(string item_name, int count)
        {
            this.item_name = item_name;
            this.count = count;
        }

        public override string str()
        {
            if (count > 1) return count + " " + item_name;
            return item_name;
        }

        public override bool in_inventory(inventory_section i)
        {
            foreach (var s in i.slots)
                if (s.item == item_name && s.count >= count)
                    return true;
            return false;
        }

        public override void on_craft(inventory_section i)
        {
            i.remove(item_name, count);
        }
    }

}

public class recipe
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
        var itm = Resources.Load<item>("items/"+product);
        ce.image.sprite = itm.sprite;
        ce.text.text = "";
        foreach (var i in ingredients)
            ce.text.text += i.str() + " + ";
        ce.text.text = ce.text.text.Substring(0, ce.text.text.Length - 2);

        if (product_count > 1) ce.text.text += "> " + product_count + " x " + product;
        else ce.text.text += "> " + product;
        return ce;
    }

    public bool can_craft(inventory_section i)
    {
        foreach (var ing in ingredients)
            if (!ing.in_inventory(i))
                return false;
        return true;
    }

    public void craft(inventory_section from, inventory_section to)
    {
        if (!can_craft(from)) return;
        foreach (var ing in ingredients)
            ing.on_craft(from);
        to.add(product, product_count);
    }
}