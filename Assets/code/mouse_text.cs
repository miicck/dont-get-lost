using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Text that follows the mouse around. </summary>
public class mouse_text : MonoBehaviour
{
    public UnityEngine.UI.Text text_element;
    RectTransform rect_transform;

    void Start()
    {
        rect_transform = GetComponent<RectTransform>();
    }

    void Update()
    {
        text_element.text = "";
        if (player.current == null) return;

        transform.SetAsLastSibling(); // Stay on top
        rect_transform.anchoredPosition = Input.mousePosition;

        var slot = utils.raycast_ui_under_mouse<inventory_slot>();
        if (slot != null && slot.contents_display_name != null)
            text_element.text = slot.contents_display_name;
        else
            text_element.text = "";
    }
}
