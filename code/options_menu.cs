using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class options_menu_option : MonoBehaviour
{
    public abstract void load_default();
    public abstract void initialize_option();
}

public class options_menu : MonoBehaviour
{
    public UnityEngine.UI.ScrollRect scroll_rect;
    public RectTransform main_menu;

    public void change_content(RectTransform content)
    {
        // Disable all content
        foreach (Transform t in scroll_rect.viewport.transform)
            t.gameObject.SetActive(false);

        // Enable the chosen content
        content.gameObject.SetActive(true);
        scroll_rect.content = content;
    }

    public void restore_default_options()
    {
        foreach (var o in GetComponentsInChildren<options_menu_option>(true))
            o.load_default();
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static UnityEngine.Rendering.Volume global_volume
    {
        get
        {
            if (_global_volume == null)
                _global_volume = FindObjectOfType<UnityEngine.Rendering.Volume>();
            return _global_volume;
        }
    }
    static UnityEngine.Rendering.Volume _global_volume;

    public void save_and_quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void initialize_options()
    {
        var om = Resources.Load<options_menu>("ui/options_menu");
        foreach (var o in om.GetComponentsInChildren<options_menu_option>(true))
            o.initialize_option();
    }

    public static void set_float(string name, float val)
    {
        UnityEngine.Rendering.HighDefinition.ColorAdjustments color_adjust;
        if (!global_volume.profile.TryGet(out color_adjust))
            throw new System.Exception("Global volume has no color adjustments override!");

        switch (name)
        {
            case "contrast":
                color_adjust.contrast.overrideState = true;
                color_adjust.contrast.value = Mathf.Clamp(val, -100f, 100f);
                break;

            case "saturation":
                color_adjust.saturation.overrideState = true;
                color_adjust.saturation.value = Mathf.Clamp(val, -100f, 100f);
                break;

            case "master_volume":
                AudioListener.volume = val;
                break;

            default:
                throw new System.Exception("Unkown float option: " + name);
        }

        PlayerPrefs.SetFloat(name, val);
    }

    public static void set_bool(string name, bool value)
    {
        switch(name)
        {
            case "water_reflections":
                water_reflections.reflections_enabled = value;
                break;

            default:
                throw new System.Exception("Unkown bool option: " + name);
        }

        PlayerPrefs.SetInt(name, value ? 1 : 0);
    }

    /// <summary> Is the options menu currently open? </summary>
    public static bool open
    {
        get => _menu != null;
        set
        {
            if (_menu == null && value)
            {
                _menu = Resources.Load<options_menu>("ui/options_menu").inst();
                _menu.transform.SetParent(FindObjectOfType<Canvas>().transform);
                _menu.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                _menu.change_content(_menu.main_menu);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                player.current.close_all_ui();
            }
            else if (!value)
            {
                Destroy(_menu.gameObject);
                _menu = null;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
    static options_menu _menu; // The actual menu object
}
