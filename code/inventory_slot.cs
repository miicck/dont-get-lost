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
                slot.create_mouse_item(false);
            else if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                slot.create_mouse_item(true);
        }
        else if (slot.accepts(mi.item))
        {
            slot.set_item_count(mi.item, slot.count + mi.count);
            mi.item = null;
        }
        else popup_message.create("Can't put that item here!");
    }
}

public class inventory_slot : MonoBehaviour, IInspectable
{
    public Image item_image;
    public Button button;
    public Text count_text;

    /// <summary> Returns true if <paramref name="item"/> can be put in this slot. </summary>
    public virtual bool accepts(item item)
    {
        if (item == null) return false;
        return this.item == null || this.item.name == item.name;
    }

    private void Start()
    {
        var isb = button.gameObject.AddComponent<inventory_slot_button>();
        isb.slot = this;
        update_ui(item, count);
    }

    /// <summary> The in-game inventory to which this ui slot belongs. </summary>
    public inventory inventory
    {
        get => _inventory;
        set
        {
            if (_inventory == value) return;
            if (_inventory != null)
                throw new System.Exception("Tried to overwrite my inventory!");
            _inventory = value;
        }
    }
    inventory _inventory;

    /// <summary> My index within the given inventory. </summary>
    public int index
    {
        get => _index;
        set
        {
            if (_index == value) return;
            if (_index >= 0)
                throw new System.Exception("Tried to overwrite slot index!");
            _index = value;
        }
    }
    int _index = -1;

    /// <summary> The networked version of this inventory slot, which is 
    /// a child of <see cref="inventory"/>. </summary>
    public inventory_slot_networked networked;

    public item item => networked?.item;
    public int count => networked == null ? 0 : networked.count;

    public delegate void func();

    List<func> listeners = new List<func>();
    public void add_on_change_listener(func f)
    {
        if (f == null) return;
        listeners.Add(f);
    }

    public void update_ui(item item, int count)
    {
        item_image.sprite = item == null ? empty_sprite() : item.sprite;
        count_text.text = item == null || count == 0 ? "" : "" + count;

        // Make the image transparent if the image is null
        var col = item_image.color;
        col.a = item_image.sprite == null ? 0f : 1f;
        item_image.color = col;
    }

    protected virtual Sprite empty_sprite()
    {
        return Resources.Load<Sprite>("sprites/ui_panel");
    }

    protected virtual void on_change()
    {
        foreach (var f in listeners) f();
        update_ui(item, count);
    }

    public void create_mouse_item(bool right_click)
    {
        if (count < 1) return;

        // Local copies for the lambda function
        int to_pickup = right_click ? Mathf.Max(count / 2, 1) : count;
        int remaining = count - to_pickup;
        item item_to_pickup = item;

        networked.delete(() =>
        {
            if (remaining > 0) create_new_networked(item, remaining);
            else networked = null;
            on_change();
            mouse_item.create(item_to_pickup, to_pickup, this);
        });
    }

    public void set_item_count(item item, int count, func on_success = null)
    {
        if (item == null || count == 0)
        {
            // Slot has been cleared, delete the networked version
            if (networked != null)
            {
                networked.delete();
                networked = null;
                on_change();
                on_success?.Invoke();
            }
            return;
        }

        if (this.item == item && this.count == count)
        {
            // No change => no need to get the network involved
            on_success?.Invoke();
            return;
        }

        // Slot contents changed, update the networked version
        if (networked == null)
        {
            create_new_networked(item, count);
            on_change();
            on_success?.Invoke();
        }
        else
        {
            if (networked.item_name == item.name)
            {
                networked.set_item_count(item, count);
                on_change();
                on_success?.Invoke();
            }
            else throw new System.Exception("Item mismatch in set_item_count!");
        }
    }

    void create_new_networked(item item, int count)
    {
        // Create the new slot
        networked = (inventory_slot_networked)client.create(
            inventory.transform.position,
            "misc/networked_inventory_slot",
            inventory.GetComponent<networked>());

        networked.index = index;
        networked.set_item_count(item, count);
        on_change();
    }

    //##############//
    // IInspectable //
    //##############//

    public string inspect_info() { return item.item_quantity_info(item, count); }
    public Sprite main_sprite() { return item?.main_sprite(); }
    public Sprite secondary_sprite() { return item?.secondary_sprite(); }

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