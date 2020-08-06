using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farming_spot : fixture_with_inventory, ILeftPlayerMenu, IInspectable
{
    networked_variables.net_int time_planted;

    recipe growing;
    item seed => ((item_ingredient)growing?.ingredients[0])?.item;
    item product => growing?.products[0].item;

    string last_product;
    GameObject grown;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        time_planted = new networked_variables.net_int();
    }

    new public string inspect_info()
    {
        return base.inspect_info() + "\n" +
               (growing == null ? "Nothing growing." :
                seed?.plural + " growing into " + product?.plural + ".") + "\n" +
                "Last product: " + last_product;
    }

    void update_growth()
    {
        if (growing == null) return;

        int delta_time = client.server_time - time_planted.value;
        int target_products = delta_time / 2;
        int in_inventory = inventory.count(product);

        if (target_products > in_inventory)
        {
            int seeds_left = inventory.count(seed);
            int to_grow = Mathf.Min(seeds_left, target_products - in_inventory);

            // Nothing to grow
            if (to_grow == 0)
                return;

            // Grow the product
            if (product != null)
            {
                last_product = product.name;

                // Add happens before remove, because if remove removes the last
                // seed, then the product becomes null (in the inventory on_change method).
                if (inventory.add(product, to_grow))
                    inventory.remove(seed, to_grow);
            }
        }
    }

    //########################//
    // fixture_with_inventory //
    //########################//

    protected override string inventory_prefab()
    {
        return "inventories/farming_spot";
    }

    protected override void on_set_inventory()
    {
        inventory.add_on_change_listener(() =>
        {
            CancelInvoke("update_growth");

            // Update the recipe that we're growing
            growing = null;
            foreach (var r in Resources.LoadAll<recipe>("recipes/farming_spots/" + name))
                if (r.can_craft(inventory))
                {
                    growing = r;
                    break;
                }

            // Create/remove the grown object
            int grown_count = last_product == null ? 0 : inventory.count(last_product);
            if (grown == null && grown_count > 0)
            {
                grown = Resources.Load<item>("items/" + last_product).inst().gameObject;
                Destroy(grown.GetComponent<item>());
                grown.transform.SetParent(transform);
                grown.transform.localPosition = Vector3.zero;
            }
            else if (grown != null && grown_count == 0)
            {
                last_product = null;
                Destroy(grown);
            }

            time_planted.value = client.server_time;
            InvokeRepeating("update_growth", 1, 1);
        });
    }


    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { update_growth(); }
    public void on_left_menu_close() { }
}
