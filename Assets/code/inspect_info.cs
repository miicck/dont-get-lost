using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inspect_info : MonoBehaviour
{
    public const float INSPECT_RANGE = 2048f;
    public UnityEngine.UI.Text info_text;
    public UnityEngine.UI.Image image_upper;
    public UnityEngine.UI.Image image_lower;

    public bool visible
    {
        get => gameObject.activeInHierarchy;
        set
        {
            IInspectable inspect = null;
            List<IAddsToInspectionText> to_add = null;

            if (value)
            {
                if (Cursor.visible)
                {
                    // Raycast for a ui element
                    inspect = utils.raycast_ui_under_mouse<IInspectable>();
                    to_add = new List<IAddsToInspectionText>(); // Impement if needed
                }
                else
                {
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
                        {
                            inspect = (IInspectable)comp;
                            to_add = new List<IAddsToInspectionText>(
                                comp.GetComponentsInChildren<IAddsToInspectionText>());
                        }
                    }
                }

                if (inspect != null)
                {
                    info_text.text = inspect.inspect_info().capitalize().Trim();
                    foreach (var ta in to_add)
                        info_text.text += "\n" + ta.added_inspection_text().Trim();

                    Debug.Log(to_add.Count);
                    Sprite main = inspect.main_sprite();
                    Sprite secondary = inspect.secondary_sprite();

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
            }
            gameObject.SetActive(inspect != null);
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