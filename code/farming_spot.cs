using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farming_spot : fixture_with_inventory, ILeftPlayerMenu, IInspectable
{
    networked_variables.net_int time_planted;

    recipe growing;
    item seed => ((item_ingredient)growing?.ingredients[0])?.item;
    item product => growing?.products[0].item;

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
                seed?.plural + " growing into " + product?.plural + ".");
    }

    void update_growth()
    {
        if (growing == null) return;
        if (inventory.count(product) > 0) return;

        int delta_time = client.server_time - time_planted.value;
        if (delta_time > 1)
        { 
            // Grow the product
            if (product != null)
            {
                // Add happens before remove, because if remove removes the last
                // seed, then the product becomes null (in the inventory on_change method).
                if (inventory.add(product, 1))
                {
                    inventory.remove(seed, 1);

                    // Create the representation of the grown product
                    if (grown != null) Destroy(grown);
                    grown = create(product.name, transform.position, transform.rotation).gameObject;
                    grown.transform.SetParent(transform);
                    Destroy(grown.GetComponent<item>());
                }
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

            // Destroy the representation of the grown product if it has been removed
            if (grown != null)
                if (inventory.count(Resources.Load<item>("items/" + grown.name)) < 1)
                    Destroy(grown);

            time_planted.value = client.server_time;
            InvokeRepeating("update_growth", 1, 1);
        });
    }


    //#################//
    // ILeftPlayerMenu //
    //#################//

    public inventory editable_inventory() { return inventory; }
    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { update_growth(); }
    public void on_left_menu_close() { }
}
