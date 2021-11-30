using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class contract : item
{
    ingredient[] ingredients => GetComponents<ingredient>();
    product[] products => GetComponents<product>();

    public string info(player player_with_contract)
    {
        Dictionary<string, int> in_use = new Dictionary<string, int>();
        string sat_string = "Requirements\n";
        foreach (var i in ingredients)
            sat_string += "  " + i.satisfaction_string(player_with_contract.inventory, ref in_use) + "\n";
        sat_string = sat_string.Trim();

        string reward_string = "Rewards\n";
        foreach (var p in products)
            reward_string += "  " + p.product_name_quantity() + "\n";
        reward_string = reward_string.Trim();

        return sat_string + "\n" + reward_string;
    }

    RectTransform create_contract_ui(contract_menu menu)
    {
        var ui = Resources.Load<RectTransform>("ui/contract").inst();

        Dictionary<string, int> in_use = new Dictionary<string, int>();
        bool completable = true;
        foreach (var i in ingredients)
        {
            if (!i.find(player.current.inventory, ref in_use))
                completable = false;
        }

        foreach (var but in ui.GetComponentsInChildren<UnityEngine.UI.Button>())
        {
            if (but.name.Contains("complete"))
            {
                but.colors = ui_colors.red_green_button_colors(completable);

                if (completable)
                    but.onClick.AddListener(() =>
                    {
                        var products_copy = products;
                        delete(() =>
                        {
                            bool success = true;
                            foreach (var kv in in_use)
                                if (!player.current.inventory.remove(kv.Key, kv.Value))
                                    success = false;

                            if (!success) Debug.LogError("Contract completed incorrectly!");

                            foreach (var p in products_copy)
                                p.create_in(player.current.inventory);

                            menu.refresh_contracts(player.current);
                        });
                    });
            }
            else if (but.name.Contains("deactivate"))
            {
                but.onClick.AddListener(() =>
                {
                    string name_copy = name;
                    delete(() =>
                    {
                        player.current.inventory.add(name_copy, 1);
                        menu.refresh_contracts(player.current);
                    });
                });
            }
        }

        foreach (var text in ui.GetComponentsInChildren<UnityEngine.UI.Text>())
            if (text.name.Contains("contract_info"))
                text.text = display_name.capitalize() + "\n" + info(player.current) + "\n";

        return ui;
    }

    //#####################//
    // Player interactions //
    //#####################//

    public override player_interaction[] item_uses()
    {
        return new player_interaction[] { new activate_contract(this) };
    }

    public class activate_contract : player_interaction
    {
        contract contract;
        public activate_contract(contract contract)
        {
            this.contract = contract;
        }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            return "activate contract";
        }

        protected override bool on_start_interaction(player player)
        {
            if (player.inventory.remove(contract, 1))
                item.create(contract.name, player.transform.position,
                    player.transform.rotation, networked: true, network_parent: player);
            return true;
        }
    }

    //################//
    // Contracts menu //
    //################//

    public class contract_menu : left_player_menu
    {
        public contract_menu() : base("active contracts") { }

        public override bool is_possible()
        {
            return player.current.contracts.Count > 0;
        }

        protected override RectTransform create_menu()
        {
            player.current.inventory.add_on_change_listener(() => refresh_contracts(player.current));
            return Resources.Load<RectTransform>("ui/active_contracts").inst();
        }

        public override bool allows_movement() { return true; }

        public void refresh_contracts(player p)
        {
            // Update the menu with the current interactions
            var content = menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

            // Delete stale contract menu items
            foreach (RectTransform t in content)
                Destroy(t.gameObject);

            // Re-create contract entries
            foreach (var c in p.contracts)
                c.create_contract_ui(this).SetParent(content);
        }

        protected override bool on_start_interaction(player player)
        {
            refresh_contracts(player);
            return base.start_interaction(player);
        }
    }
}
