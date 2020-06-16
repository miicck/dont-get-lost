using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class inventory_slot_button : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    public inventory_slot slot;

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        var mi = FindObjectOfType<mouse_item>();
        if (mi == null)
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
            {
                mi = mouse_item.create(slot.item, slot.count, slot);
                slot.clear();
            }
            else if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            {
                int pickup = slot.count > 1 ? slot.count / 2 : 1;
                mi = mouse_item.create(slot.item, pickup, slot);
                slot.set_item_count(slot.item, slot.count - pickup);
            }
        }
        else if (mi.item == slot.item || slot.item == null || slot.count == 0)
        {
            if (slot.accepts(mi.item))
            {
                slot.set_item_count(mi.item, slot.count + mi.count);
                mi.item = null;
            }
            else popup_message.create("Can't put that item here!");
        }
    }
}

public class inventory_slot : MonoBehaviour, IInspectable
{
    protected inventory_section[] sections_belonging_to { get => GetComponentsInParent<inventory_section>(true); }

    public virtual bool accepts(string item_name) { return true; }

    protected virtual void on_change() { }

    protected virtual Sprite empty_sprite() { return Resources.Load<Sprite>("sprites/inventory_slot"); }

    item _item = null;
    public string item
    {
        get => _item == null ? null : _item.name;
    }

    public int count
    {
        get; private set;
    }

    public void set_item_count(string item, int count)
    {
        if (this.item == item && this.count == count)
            return; // No change

        // If count = 0 or item = null set both to be true
        if (item == null) count = 0;
        if (count == 0) item = null;

        this.count = count;

        if (item == null)
        {
            // Clear the item + image
            _item = null;
            item_image.sprite = empty_sprite();
        }
        else
        {
            // Load the item + image
            _item = Resources.Load<item>("items/" + item);
            if (_item == null)
            {
                Debug.Log("Unkown item: " + item);
                set_item_count(null, 0);
                return;
            }
            item_image.sprite = _item.sprite;
        }

        // If sprite == null, set the item image to be completely transparent
        // (don't disable it, in case it still needs to accept graphics raycasts)
        var image_col = item_image.color;
        image_col.a = item_image.sprite == null ? 0f : 1f;
        item_image.color = image_col;

        // Set the count text
        count_text.text = count > 1 ? utils.int_to_quantity_string(count) : "";

        // Call the on_change function
        foreach (var s in sections_belonging_to)
            s.on_change();

        // Call implementation-specific on_change function
        on_change();
    }

    public void clear() { set_item_count(null, 0); }

    public Image item_image;
    public Button button;
    public Text count_text;

    private void Start()
    {
        var isb = button.gameObject.AddComponent<inventory_slot_button>();
        isb.slot = this;

        // Ensure images etc are loaded correctly
        set_item_count(item, count);
    }

    //##############//
    // IInspectable //
    //##############//

    public static string item_quantity_info(item item, int quantity)
    {
        // Titlle
        string info = (quantity < 2 ? item.display_name :
            (utils.int_to_comma_string(quantity) + " " + item.plural)) + "\n";

        // Value
        if (quantity > 1)
            info += "  Value : " + (item.value * quantity).qs() + " (" + item.value.qs() + " each)\n";
        else
            info += "  Value : " + item.value.qs() + "\n";

        // Tool type + quality
        if (item is tool)
        {
            var t = (tool)item;
            info += "  Tool type : " + tool.type_to_name(t.type) + "\n";
            info += "  Quality : " + tool.quality_to_name(t.quality) + "\n";
        }

        // Fuel value
        if (item.fuel_value > 0)
        {
            if (quantity > 1)
                info += "  Fuel value : " + (item.fuel_value * quantity).qs() + " (" + item.fuel_value.qs() + " each)\n";
            else
                info += "  Fuel value : " + item.fuel_value.qs() + "\n";
        }

        return utils.allign_colons(info);
    }

    public string inspect_info()
    {
        if (item == null) return "Empty slot";
        var itm = Resources.Load<item>("items/" + item);
        return item_quantity_info(itm, count);
    }

    public Sprite main_sprite()
    {
        if (item == null) return null;
        return Resources.Load<item>("items/" + item).main_sprite();
    }

    public Sprite secondary_sprite()
    {
        if (item == null) return null;
        return Resources.Load<item>("items/" + item).secondary_sprite();
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(inventory_slot))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var slot = (inventory_slot)target;
            UnityEditor.EditorGUILayout.TextArea(
                "Item: " + slot.item + "\n" +
                "Count: " + slot.count);
        }
    }
#endif
}
