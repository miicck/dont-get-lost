using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farming_spot : fixture, ILeftPlayerMenu, IInspectable
{
    public inventory inventory { get; private set; }

    networked_variables.net_int time_planted;

    recipe growing;
    item seed => ((item_ingredient)growing?.ingredients[0])?.item;
    item product => growing?.products[0].item;

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

    public override void on_first_register()
    {
        base.on_first_register();
        client.create(transform.position, "inventories/farming_spot", this);
    }

    item grown // this shouldn't be an item, so we can't click to delete it
    {
        get => _grown;
        set
        {
            if (_grown != null)
                Destroy(_grown.gameObject);

            if (value == null)
            {
                _grown = null;
                return;
            }

            _grown = value.inst();
            _grown.transform.SetParent(transform);
            _grown.transform.localPosition = Vector3.zero;
        }
    }
    item _grown;

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
        {
            inventory = (inventory)child;

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

                // Remove grown object if it no longer exists 
                if (grown != null)
                {
                    int grown_count = inventory.count(grown);
                    if (grown_count == 0)
                        grown = null;
                }

                time_planted.value = client.server_time;
                InvokeRepeating("update_growth", 1, 1);
            });
        }
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

            inventory.remove(seed, to_grow);
            inventory.add(product, to_grow);
            grown = product;
        }
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { update_growth(); }
    public void on_left_menu_close() { }
}
