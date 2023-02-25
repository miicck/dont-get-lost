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
            "pressing " + controls.bind_name(controls.BIND.INSPECT) + ". This also " +
            "works when hovering over items in your inventory.");
    }

    public void turn_on(string text, Sprite primary, Sprite secondary)
    {
        gameObject.SetActive(true); 
        info_text.text = text;
        image_upper.sprite = primary;
        image_upper.enabled = primary != null;
        image_lower.sprite = secondary;
        image_lower.enabled = secondary != null;

        foreach(var lg in GetComponentsInChildren<UnityEngine.UI.HorizontalOrVerticalLayoutGroup>())
        {
            // Force re-evaluation of layout groups
            // to make inspect window expand to text
            lg.enabled = false;
            lg.enabled = true;
        }
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
    public player_inspectable(Transform attached_to) => transform = attached_to;

    public delegate string text_func();
    public text_func text;

    public delegate Sprite sprite_func();
    public sprite_func sprite;
    public sprite_func secondary_sprite;

    public override controls.BIND keybind => controls.BIND.INSPECT;
    public override string context_tip() => "inspect";
    public override bool simultaneous() => true;

    void update()
    {
        if (transform == null) return; // Deleted

        IAddsToInspectionText[] added_inspect =
            transform.GetComponentInChildren<inventory_slot>()?.
            item_in_slot?.
            GetComponentsInChildren<IAddsToInspectionText>();

        if (added_inspect == null || added_inspect.Length == 0)
            added_inspect = transform.GetComponentsInChildren<IAddsToInspectionText>();

        string str = text?.Invoke();
        foreach (var add in added_inspect)
        {
            var txt = add.added_inspection_text();
            if (txt == null) continue;
            str += "\n" + txt.Trim();
        }

        inspect_info.turn_on(str, sprite?.Invoke(), secondary_sprite?.Invoke());
        
    }

    protected override bool on_start_interaction(player player)
    {
        update();
        return !controls.held(controls.BIND.INSPECT);
    }

    public override bool continue_interaction(player player)
    {
        if (transform == null) return true; // Deleted
        update();
        return !controls.held(controls.BIND.INSPECT);
    }

    protected override void on_end_interaction(player player)
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
                _inspect_info = Resources.Load<inspect_info>("ui/inspect_info").inst(game.canvas.transform);
                _inspect_info.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            return _inspect_info;
        }
    }
    static inspect_info _inspect_info;
}
