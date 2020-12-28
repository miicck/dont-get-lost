using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pinned_message : MonoBehaviour
{
    UnityEngine.UI.Text text;
    UnityEngine.UI.Image image;
    RectTransform ui;

    public string message
    {
        get => text?.text;
        set
        {
            // I've been destroyed, don't create the ui
            if (this == null) return;

            if (ui == null)
            {
                // Create the ui
                ui = Resources.Load<RectTransform>("ui/pinned_message").inst();
                ui.SetParent(FindObjectOfType<game>().main_canvas.transform);

                foreach (RectTransform child in ui)
                {
                    image = child.GetComponent<UnityEngine.UI.Image>();
                    if (image != null)
                        break;
                }

                text = ui.GetComponentInChildren<UnityEngine.UI.Text>();

                messages.Add(this);
                locate_messages();
            }

            text.text = value;
        }
    }

    public Color color
    {
        get => image == null ? Color.white : image.color;
        set
        {
            if (image != null)
                image.color = value;
        }
    }

    private void OnDestroy()
    {
        if (ui == null) return;

        messages.Remove(this);
        locate_messages();

        Destroy(ui.gameObject);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<pinned_message> messages;

    public static void initialize()
    {
        messages = new HashSet<pinned_message>();
    }

    static void locate_messages()
    {
        float y = 0;
        foreach (var m in messages)
        {
            m.ui.anchoredPosition = new Vector2(0, y);
            y -= m.ui.sizeDelta.y;
        }
    }
}

public static class pinned_message_extensions
{
    public static pinned_message add_pinned_message(this GameObject g, string message, Color color)
    {
        var pm = g.AddComponent<pinned_message>();
        pm.message = message;
        pm.color = color;
        return pm;
    }
}
