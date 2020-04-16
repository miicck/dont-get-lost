using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class mouse_item : MonoBehaviour
{
    item _item;
    public item item
    {
        get { return _item; }
        set
        {
            _item = value;
            if (_item == null) Destroy(gameObject);
            else item_image.sprite = _item.sprite;
        }
    }

    int _count;
    public int count
    {
        get { return _count; }
        set
        {
            _count = value;
            if (_count < 1) Destroy(gameObject);
            else item_count_text.text = _count > 1 ? "" + _count : "";
        }
    }

    public Text item_count_text;
    public Image item_image;
    inventory_slot origin; 

    private void Update()
    {
        if (!Cursor.visible)
        {
            if (origin.item != null && origin.item != item)
                Debug.LogError("Origin item has changed, I don't know what to do!");

            origin.item = item;
            origin.count += count;
            count = 0;
        }

        transform.position = Input.mousePosition;
    }

    public static mouse_item create(item i, int count, inventory_slot origin)
    {
        var ret = Resources.Load<mouse_item>("ui/mouse_item").inst();
        ret.transform.SetParent(FindObjectOfType<Canvas>().transform);
        ret.origin = origin;
        ret.item = i;
        ret.count = count;
        ret.transform.position = Input.mousePosition;
        return ret;
    }
}
