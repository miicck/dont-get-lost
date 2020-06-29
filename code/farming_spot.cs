using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farming_spot : fixture, ILeftPlayerMenu
{
    public inventory inventory { get; private set; }

    networked_variables.net_int time_planted;
    seed growing;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        time_planted = new networked_variables.net_int();
    }

    public override void on_first_register()
    {
        base.on_first_register();
        client.create(transform.position, "inventories/farming_spot", this);
    }

    GameObject grown;

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
        {
            inventory = (inventory)child;

            inventory.add_on_change_listener(() =>
            {
                if (grown != null) Destroy(grown);
                growing = null;
                CancelInvoke("update_growth");

                foreach (var kv in inventory.contents())
                    if (kv.Key is seed)
                    {
                        growing = (seed)kv.Key;
                        time_planted.value = client.server_time;
                        break;
                    }

                if (growing != null && inventory.count(growing.grows_into) > 0)
                {
                    grown = growing.grows_into.inst().gameObject;
                    Destroy(grown.GetComponent<item>());
                    grown.transform.SetParent(transform);
                    grown.transform.localPosition = Vector3.zero;
                }

                if (growing != null)
                    InvokeRepeating("update_growth", growing.growth_time + 1, growing.growth_time + 1);
            });
        }
    }

    void update_growth()
    {
        if (growing == null) return;

        int delta_time = client.server_time - time_planted.value;
        int in_inventory = inventory.count(growing.grows_into);
        int target = delta_time / growing.growth_time;

        if (target > in_inventory)
        {
            int seeds_left = inventory.count(growing);
            int to_grow = Mathf.Min(seeds_left, target - in_inventory);

            inventory.remove(growing, to_grow);
            inventory.add(growing.grows_into, to_grow);
        }
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { update_growth(); }
    public void on_left_menu_close() { }
}
