using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shop_slot : MonoBehaviour, IInspectable
{
    public UnityEngine.UI.Text count_text;
    public UnityEngine.UI.Text buy_price_text;
    public UnityEngine.UI.Text sell_price_text;
    public UnityEngine.UI.Button buy_button;
    public UnityEngine.UI.Button sell_button;
    public UnityEngine.UI.Image image;

    public float markup = 0.2f;

    public item item
    {
        get => _item;
        set
        {
            _item = value;
            setup();
        }
    }
    item _item;

    public int stock
    {
        get => _stock;
        set
        {
            if (value < 0) value = 0;
            if (_stock == value)
                return; // No change
            _stock = value;
            count_text.text = utils.int_to_quantity_string(value);
            on_change();
        }
    }
    int _stock = 0;

    public int buy_price
    {
        get => _buy_price;
        private set
        {
            if (value < 1) value = 1;
            _buy_price = value;
            buy_price_text.text = utils.int_to_quantity_string(value);
        }
    }
    public int _buy_price;

    public int sell_price
    {
        get => _sell_price;
        private set
        {
            if (value < 1) value = 1;
            _sell_price = value;
            sell_price_text.text = utils.int_to_quantity_string(value);
        }
    }
    public int _sell_price;

    void setup()
    {
        if (item == null)
        {
            image.sprite = null;
            image.enabled = false;
            stock = 0;
            buy_price = 0;
            sell_price = 0;
            return;
        }

        image.sprite = item.sprite;
        image.enabled = true;

        buy_price = Mathf.CeilToInt(item.value * (1 + markup));
        sell_price = Mathf.FloorToInt(item.value * (1 - markup));

        buy_button.onClick.RemoveAllListeners();
        sell_button.onClick.RemoveAllListeners();

        buy_button.onClick.AddListener(() =>
        {
            if (stock < 1)
            {
                popup_message.create(item.plural + " are out of stock!");
                return;
            }

            if (player.current.inventory.count("coin") >= buy_price)
            {
                player.current.inventory.remove("coin", buy_price);
                player.current.inventory.add(item.name, 1);
                stock -= 1;
            }
            else
            {
                string msg = "You do not have enough coins to purchase ";
                msg += utils.a_or_an(item.display_name) + " " + item.display_name;
                popup_message.create(msg);
            }
        });

        sell_button.onClick.AddListener(() =>
        {
            if (player.current.inventory.count(item.name) > 0)
            {
                player.current.inventory.remove(item.name, 1);
                player.current.inventory.add("coin", sell_price);
                stock += 1;
            }
            else
            {
                string msg = "You do not have ";
                msg += utils.a_or_an(item.display_name) + " ";
                msg += item.display_name + " to sell!";
                popup_message.create(msg);
            }
        });
    }

    public delegate void on_change_func();
    List<on_change_func> on_change_listeners = new List<on_change_func>();

    public void add_on_change_listener(on_change_func f)
    {
        if (f == null) return;
        on_change_listeners.Add(f);
    }

    void on_change()
    {
        foreach (var f in on_change_listeners) f();
    }

    private void OnValidate()
    {
        setup();
    }

    public string inspect_info()
    {
        return inventory_slot.item_quantity_info(item, 1);
    }

    public Sprite main_sprite() { return item.sprite; }
    public Sprite secondary_sprite() { return null; }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(shop_slot))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ss = (shop_slot)target;

            var new_item = (item)UnityEditor.EditorGUILayout.ObjectField(
                "item", ss.item, typeof(item), false);

            if (new_item != ss.item)
            {
                ss.item = new_item;
                UnityEditor.EditorUtility.SetDirty(ss);
            }

            base.OnInspectorGUI();
        }
    }
#endif
}
