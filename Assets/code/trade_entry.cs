using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class trade_entry : MonoBehaviour
{
    public UnityEngine.UI.Image sprite;
    public UnityEngine.UI.Text item_text;

    public UnityEngine.UI.Text buy_price_text;
    public UnityEngine.UI.Button buy_button;
    public UnityEngine.UI.Button buy_more_button;

    public UnityEngine.UI.Text sell_price_text;
    public UnityEngine.UI.Button sell_button;
    public UnityEngine.UI.Button sell_more_button;

    public UnityEngine.UI.Text player_stock_text;
    public UnityEngine.UI.Text stock_text;

    void set_stock_text()
    {
        stock_text.text = stock.ToString();
        if (delta_stock > 0) stock_text.text += "+" + delta_stock;
        else if (delta_stock < 0) stock_text.text += delta_stock;

        player_stock_text.text = player_stock.ToString();
        if (delta_stock > 0) player_stock_text.text += "-" + delta_stock;
        else if (delta_stock < 0) player_stock_text.text += "+" + (-delta_stock);
    }

    /// <summary> The item being traded. </summary>
    public item item { get; private set; }

    /// <summary> How much of the item the trader has. </summary>
    public int stock
    {
        get => _stock;
        set
        {
            _stock = value;
            if (_stock < 0) _stock = 0;
            if (delta_stock < -_stock)
                delta_stock = -_stock;
            set_stock_text();
        }
    }
    int _stock;

    /// <summary> How much of this item the player has. </summary>
    public int player_stock
    {
        get => _player_stock;
        set
        {
            _player_stock = value;
            if (_player_stock < 0) _player_stock = 0;
            set_stock_text();
        }
    }
    int _player_stock;

    /// <summary> How much the stock changes given the proposed trade. </summary>
    public int delta_stock
    {
        get => _delta_stock;
        private set
        {
            _delta_stock = value;
            if (_delta_stock < -_stock) _delta_stock = -_stock;
            if (_delta_stock > player_stock) _delta_stock = player_stock;
            set_stock_text();
            on_change?.Invoke();
        }
    }
    int _delta_stock;

    /// <summary> How much the trader will buy the item for. </summary>
    public int buy_price
    {
        get => _buy_price;
        private set
        {
            _buy_price = value;
            if (_buy_price < 1) _buy_price = 1;
            buy_price_text.text = _buy_price + " coin/ea";
        }
    }
    int _buy_price;

    /// <summary> How much the trader will sell the item for. </summary>
    public int sell_price
    {
        get => _sell_price;
        private set
        {
            _sell_price = value;
            if (_sell_price < 1) _sell_price = 1;
            sell_price_text.text = _sell_price + " coin/ea";
        }
    }
    int _sell_price;

    public void initialize(item item, int stock)
    {
        this.item = item;
        this.stock = stock;
        sprite.sprite = item.sprite;
        item_text.text = item.plural;
        buy_price = item.value;
        sell_price = (int)(item.value * 1.2f);

        buy_button.onClick.AddListener(() => delta_stock -= 1);
        buy_more_button.onClick.AddListener(() => delta_stock -= 10);

        sell_button.onClick.AddListener(() => delta_stock += 1);
        sell_more_button.onClick.AddListener(() => delta_stock += 10);
    }

    public void carry_out(player player, trader trader)
    {
        if (delta_stock > 0)
        {
            // Trader bought stock, give the player coins/remove items
            int stock_bought = delta_stock;
            if (player.inventory.remove(item, stock_bought))
                player.inventory.add("coin", stock_bought * buy_price);

            // Give the trader items/remove coins
            trader.set_stock(item.name, stock + stock_bought);
            trader.set_stock("coin", trader.get_stock("coin") - stock_bought * buy_price);
        }
        else if (delta_stock < 0)
        {
            // Trader sold stock, give the player items/remove coins
            int stock_sold = -delta_stock;
            if (player.inventory.remove("coin", stock_sold * sell_price))
                player.inventory.add(item, stock_sold);

            // Give the trader coins/remove stock
            trader.set_stock(item.name, stock - stock_sold);
            trader.set_stock("coin", trader.get_stock("coin") + stock_sold * sell_price);
        }

        // This will trigger an update to the new trader stock value
        delta_stock = 0; 
    }

    public delegate void on_change_listener();
    public on_change_listener on_change;
}
