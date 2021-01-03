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

    public struct inspectable_info
    {
        public IInspectable inspecting;
        public List<IAddsToInspectionText> sub_inspecting;
    }

    public static inspectable_info inspectable_under_cursor
    {
        get
        {
            // Raycast for a ui element
            if (Cursor.visible)
                return new inspectable_info
                {
                    inspecting = utils.raycast_ui_under_mouse<IInspectable>(),
                    sub_inspecting = new List<IAddsToInspectionText>()
                };

            // Raycast for the nearest collider, and search that for IInspectable
            // objects (raycast for collider, rather than IInspectable so that 
            // you can't get IInspectables from behind stuff).
            RaycastHit hit;
            float range;
            var ray = player.current.camera_ray(INSPECT_RANGE, out range);
            var col = utils.raycast_for_closest<Collider>(ray, out hit, range,
                (c) => !c.transform.IsChildOf(player.current.transform));

            if (col != null)
            {
                var comp = col.gameObject.GetComponentInParent(typeof(IInspectable));
                if (comp != null)
                    return new inspectable_info
                    {
                        inspecting = (IInspectable)comp,
                        sub_inspecting = new List<IAddsToInspectionText>(
                        comp.GetComponentsInChildren<IAddsToInspectionText>())
                    };
            }

            return new inspectable_info();
        }
    }

    public bool visible
    {
        get => gameObject.activeInHierarchy;
        set
        {
            if (value)
            {
                var info = inspectable_under_cursor;
                if (info.inspecting != null)
                {
                    info_text.text = info.inspecting.inspect_info().capitalize().Trim();
                    foreach (var ta in info.sub_inspecting)
                    {
                        var added_text = ta.added_inspection_text();
                        added_text = added_text?.Trim();
                        if (added_text != null && added_text.Length > 0)
                            info_text.text += "\n" + added_text;
                    }
                    Sprite main = info.inspecting.main_sprite();
                    Sprite secondary = info.inspecting.secondary_sprite();

                    if (main == null)
                        image_upper.enabled = false;
                    else
                    {
                        image_upper.enabled = true;
                        image_upper.sprite = main;
                    }

                    if (secondary == null)
                        image_lower.enabled = false;
                    else
                    {
                        image_lower.enabled = true;
                        image_lower.sprite = secondary;
                    }
                }

                gameObject.SetActive(info.inspecting != null);
            }
            else gameObject.SetActive(false);
        }
    }
}

public interface IInspectable
{
    string inspect_info();
    Sprite main_sprite();
    Sprite secondary_sprite();
}

public interface IAddsToInspectionText
{
    string added_inspection_text();
}