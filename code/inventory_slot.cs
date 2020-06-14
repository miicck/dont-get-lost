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

public class inventory_slot : MonoBehaviour
{
    inventory_section[] sections_belonging_to { get => GetComponentsInParent<inventory_section>(true); }

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
}
