using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class trader : MonoBehaviour, IPlayerInteractable
{
    public abstract string display_name();
    public abstract int get_stock(string item);
    public abstract Dictionary<string, int> get_stock();
    public abstract void set_stock(string item, int count);

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        trade.trader = this;
        return new player_interaction[] { trade };
    }

    trade_intearction trade { get; } = new trade_intearction();

    class trade_intearction : player_interaction
    {
        public trader trader;
        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() { return "Trade"; }
        public override bool allows_mouse_look() { return false; }
        public override bool allows_movement() { return false; }

        RectTransform ui;
        UnityEngine.UI.Text player_coins_text;
        UnityEngine.UI.Text trader_coins_text;
        bool interaction_completed = false;

        int current_trade_value()
        {
            // Update the total value of goods transfered
            int player_coins_gained = 0;
            foreach (var te in ui.GetComponentsInChildren<trade_entry>())
            {
                // Determine how many coins the player gains or looses for this trade
                if (te.delta_stock > 0) player_coins_gained += te.delta_stock * te.buy_price;
                else if (te.delta_stock < 0) player_coins_gained += te.delta_stock * te.sell_price;
            }
            return player_coins_gained;
        }

        void update_ui(player player)
        {
            // Update how much the player has of each item
            foreach (var te in ui.GetComponentsInChildren<trade_entry>())
            {
                te.player_stock = player.inventory.count(te.item);
                te.stock = trader.get_stock(te.item.name);
            }

            // Update trade value
            int player_coins_gained = current_trade_value();

            // Update player coins text with the delta amount
            player_coins_text.text = player.inventory.count("coin").ToString();
            if (player_coins_gained > 0) player_coins_text.text += "+" + player_coins_gained.qs();
            else if (player_coins_gained < 0) player_coins_text.text += "-" + (-player_coins_gained).qs();

            // Update traader cons text with the delta amount
            trader_coins_text.text = trader.get_stock("coin").ToString();
            if (player_coins_gained > 0) trader_coins_text.text += "-" + player_coins_gained.qs();
            else if (player_coins_gained < 0) trader_coins_text.text += "+" + (-player_coins_gained).qs();
        }

        public override bool start_interaction(player player)
        {
            if (ui == null)
            {
                // Create/position the ui
                ui = Resources.Load<RectTransform>("ui/trade_window").inst();
                ui.SetParent(game.canvas.transform);
                ui.anchoredPosition = Vector2.zero;

                // Get the fields for the player name, trader name and how many coins each have
                var player_name = ui.Find("header").Find("player_name").GetComponent<UnityEngine.UI.Text>();
                var trader_name = ui.Find("header").Find("trader_name").GetComponent<UnityEngine.UI.Text>();
                player_coins_text = player_name.transform.Find("coins_text").GetComponent<UnityEngine.UI.Text>();
                trader_coins_text = trader_name.transform.Find("coins_text").GetComponent<UnityEngine.UI.Text>();

                // Initialise the above fields
                player_name.text = player.player_username;
                trader_name.text = trader.display_name();
                player_coins_text.text = player.inventory.count("coin").ToString();
                trader_coins_text.text = trader.get_stock("coin").ToString();

                // Create the trade entries from the template
                var entry_template = ui.GetComponentInChildren<trade_entry>();
                foreach (var kv in trader.get_stock())
                {
                    // Don't allow direct trade of coins
                    if (kv.Key == "coin") continue;

                    // Create, position and initialize the trade entry for this item
                    var item = Resources.Load<item>("items/" + kv.Key);
                    var entry = entry_template.inst();
                    entry.transform.SetParent(entry_template.transform.parent);
                    entry.initialize(item, kv.Value);
                    entry.on_change = () => update_ui(player);
                }

                // Destroy the entry template (unparent first so it doesn't appear momentarily)
                entry_template.transform.SetParent(null);
                Destroy(entry_template);

                ui.Find("confirm").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                {
                    int value = current_trade_value();
                    if (value == 0)
                    {
                        // Nothing traded => complete immediately
                        interaction_completed = true;
                        return;
                    }

                    // Check player has enough money
                    if (value < -player.inventory.count("coin"))
                    {
                        popup_message.create("You do not have enough coins to cover this transaction!");
                        return;
                    }

                    // Check trader has enough money
                    if (value > trader.get_stock("coin"))
                    {
                        popup_message.create("The trader does not have enough coins to cover this transaction!");
                        return;
                    }

                    // Carry out the trade
                    foreach (var te in ui.GetComponentsInChildren<trade_entry>())
                        te.carry_out(player, trader);

                    // Complete the interaction
                    interaction_completed = true;
                });

                ui.Find("cancel").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                {
                    interaction_completed = true;
                });
            }
            else update_ui(player); // ui already exists, ensure up to date

            // Open the menu/enable the mouse
            ui.gameObject.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return false;
        }

        public override bool continue_interaction(player player)
        {
            // Check if interaction is complete
            if (interaction_completed)
            {
                // Reset flag/end interaction
                interaction_completed = false;
                return true;
            }

            // End interaction if re-triggered
            return triggered(player);
        }

        public override void end_interaction(player player)
        {
            // Close the menu/disable the mouse
            ui.gameObject.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
