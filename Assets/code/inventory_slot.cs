using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class inventory_slot_button : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    public int index;
    public inventory inventory;

    /// <summary> Forward mouse events to the inventory. </summary>
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
            inventory.click_slot(index, false);
        else if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            inventory.click_slot(index, true);

        // An inventory slot was clicked => safe to clear greyed out crafting options
        if (inventory.transform.IsChildOf(player.current.transform))
            player.current.crafting_menu.ui.GetComponentInChildren<crafting_input>().clear_greyed_out();
    }
}

/// <summary> The UI version of an inventory slot. </summary>
public class inventory_slot : MonoBehaviour, IPlayerInteractable
{
    public Image item_image;
    public Button button;
    public Text count_text;

    item item;
    int count;

    public string contents_display_name
    {
        get
        {
            if (count == 0) return null;
            return count > 1 ? item?.plural : item?.display_name;
        }
    }

    public virtual void update(item item, int count, inventory inventory)
    {
        if (count == 0 || item == null)
        {
            // Nothing in this slot, set the ui appropriately
            item_image.sprite = empty_sprite();
            count_text.text = "";
        }
        else
        {
            // Set the UI to reflect the given item + count
            item_image.sprite = item.sprite;
            count_text.text = count.qs();
        }

        // If the sprite == null, make the image transparent
        var col = item_image.color;
        col.a = item_image.sprite == null ? 0f : 1f;
        item_image.color = col;

        this.item = item;
        this.count = count;
    }

    /// <summary> The sprite used in <see cref="item_image"/> when the slot is empty. </summary>
    public virtual Sprite empty_sprite() { return Resources.Load<Sprite>("sprites/ui_panel"); }

    /// <summary> Returns true if <paramref name="item"/> can be put in this slot. </summary>
    public virtual bool accepts(item item) { return true; }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] {new player_inspectable(transform)
        {
            text = () => item.item_quantity_info(item, count),
            sprite = () => item_image.sprite
        }};
    }
}