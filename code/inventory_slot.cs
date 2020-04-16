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
                slot.count = 0;
            }
            else if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            {
                int pickup = slot.count > 1 ? slot.count / 2 : 1;
                mi = mouse_item.create(slot.item, pickup, slot);
                slot.count -= pickup;
            }
        }
        else if (mi.item == slot.item || slot.item == null || slot.count == 0)
        {
            slot.item = mi.item;
            slot.count += mi.count;
            mi.item = null;
        }
    }
}

public class inventory_slot : MonoBehaviour
{
    item _item;
    public item item
    {
        get { return _item; }
        set
        {
            _item = value;
            if (value == null)
            {
                item_image.sprite = Resources.Load<Sprite>("sprites/inventory_slot");
                count_text.text = "";
                _count = 0;
            }
            else
            {
                item_image.enabled = true;
                item_image.sprite = _item.sprite;
            }
        }
    }

    int _count = 1;
    public int count
    {
        get { return _count; }
        set
        {
            _count = value;
            if (_count < 1)
            {
                item = null;
                _count = 0;
            }
            count_text.text = _count > 1 ? "" + utils.int_to_quantity_string(_count) : "";
        }
    }

    public Image item_image;
    public Button button;
    public Text count_text;

    private void Start()
    {
        var isb = button.gameObject.AddComponent<inventory_slot_button>();
        isb.slot = this;

        item = item.load_from_id(item.id_from_name("log"));
        count = 100;
    }
}
