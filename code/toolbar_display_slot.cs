using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class toolbar_display_slot : MonoBehaviour
{
    public int number;
    public UnityEngine.UI.Text count_text;
    public UnityEngine.UI.Image sprite;
    public UnityEngine.UI.Image background;

    float default_background_alpha;

    static Dictionary<int, toolbar_display_slot> slots =
        new Dictionary<int, toolbar_display_slot>();

    public static void update(int toolbar_number, item item, int count)
    {
        if (!slots.TryGetValue(toolbar_number, out toolbar_display_slot found))
            return;

        found.count_text.text = count > 1 ? count.qs() : "";
        found.sprite.sprite = item?.sprite;
        found.sprite.enabled = item != null && count > 0;
    }

    public static void update_selected(int selected_slot)
    {
        foreach (var kv in slots)
        {
            Color reset = kv.Value.background.color;
            reset.a = kv.Value.default_background_alpha;
            kv.Value.background.color = reset;
        }

        if (slots.TryGetValue(selected_slot, out toolbar_display_slot found))
        {
            Color highlighted = found.background.color;
            highlighted.a = 0.5f;
            found.background.color = highlighted;
        }
    }

    public static bool toolbar_active
    {
        get => slots[1].transform.parent.gameObject.activeInHierarchy;
        set => slots[1].transform.parent.gameObject.SetActive(value);
    }

    void Start()
    {
        slots[number] = this;
        default_background_alpha = background.color.a;

        // Start empty
        count_text.text = "";
        sprite.sprite = null;
        sprite.enabled = false;
    }
}
