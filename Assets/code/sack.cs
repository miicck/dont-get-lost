using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sack : networked, IPlayerInteractable
{
    inventory inventory;

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_string display_name;

    public override void on_init_network_variables()
    {
        display_name = new networked_variables.net_string("sack");
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        if (child is inventory)
            inventory = (inventory)child;
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    player_interaction[] interactions;

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        if (interactions == null) interactions =
            new player_interaction[]
            {
                new menu(this),
                new player_inspectable(transform)
                {
                    text = () => display_name.value
                }
            };

        return interactions;
    }

    class menu : left_player_menu
    {
        sack sack;
        public menu(sack sack) : base(sack.display_name.value) => this.sack = sack;
        protected override RectTransform create_menu(Transform parent) => sack.inventory.ui;
        public override inventory editable_inventory() => sack.inventory;
        protected override void on_open() => menu.GetComponentInChildren<UnityEngine.UI.Text>().text = sack.display_name.value;
        protected override void on_close() { if (sack.inventory.is_empty()) sack.delete(); }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static sack create(Vector3 location,
        IEnumerable<KeyValuePair<item, int>> contents = null,
        string display_name = "sack")
    {
        var sack = client.create(location, "misc/sack").GetComponent<sack>();
        sack.display_name.value = display_name;

        sack.add_register_listener(() =>
        {
            client.create(location, "inventories/sack", sack);

            if (contents != null)
                sack.inventory.add_register_listener(() =>
                {
                    foreach (var kv in contents)
                        sack.inventory.add(kv.Key, kv.Value);
                });
        });

        return sack;
    }
}
