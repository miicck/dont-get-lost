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

        var elements = utils.raycast_all_ui_under_mouse<IMouseTextUI>();
        text_element.text = "";
        foreach (var s in elements)
            text_element.text += s.mouse_ui_text() + "\n";
    }
}

public interface IMouseTextUI
{
    public string mouse_ui_text();
}