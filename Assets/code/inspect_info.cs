using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inspect_info : MonoBehaviour
{
    public const float INSPECT_RANGE = 2048f;
    public UnityEngine.UI.Text info_text;
    public UnityEngine.UI.Image image_upper;
    public UnityEngine.UI.Image image_lower;

    static inspect_info()
    {
        tips.add("You can inspect the object you are currently looking at by " +
            "pressing " + controls.current_bind(controls.BIND.INSPECT) + ". This also " +
            "works when hovering over items in your inventory.");
    }

    public void turn_on(string text, Sprite primary, Sprite secondary)
    {
        info_text.text = text;
        image_upper.sprite = primary;
        image_upper.enabled = primary != null;
        image_lower.sprite = secondary;
        image_lower.enabled = secondary != null;
        gameObject.SetActive(true);
    }

    public void turn_off()
    {
        gameObject.SetActive(false);
    }
}

public interface IAddsToInspectionText
{
    public string added_inspection_text();
}

public class player_inspectable : player_interaction
{
    Transform transform;
    public player_inspectable(Transform attached_to)
    {
        transform = attached_to;
    }

    public delegate string text_func();
    public text_func text;

    public delegate Sprite sprite_func();
    public sprite_func sprite;
    public sprite_func secondary_sprite;

    public override string context_tip()
    {
        return "Press " + controls.current_bind(controls.BIND.INSPECT) + " to inspect";
    }

    public override bool conditions_met()
    {
        return controls.key_down(controls.BIND.INSPECT);
    }

    public override bool start_interaction(player player)
    {
        string str = text?.Invoke();
        foreach (var add in transform.GetComponentsInChildren<IAddsToInspectionText>())
            str += "\n" + add.added_inspection_text();
        inspect_info.turn_on(str, sprite?.Invoke(), secondary_sprite?.Invoke());
        return !controls.key_down(controls.BIND.INSPECT);
    }

    public override bool continue_interaction(player player)
    {
        return !controls.key_down(controls.BIND.INSPECT);
    }

    public override void end_interaction(player player)
    {
        inspect_info.turn_off();
    }

    static inspect_info inspect_info
    {
        get
        {
            // Create the inspect_info object if it doesn't already exist
            if (_inspect_info == null)
            {
                _inspect_info = Resources.Load<inspect_info>("ui/inspect_info").inst();
                _inspect_info.transform.SetParent(Object.FindObjectOfType<game>().main_canvas.transform);
                _inspect_info.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            return _inspect_info;
        }
    }
    static inspect_info _inspect_info;
}