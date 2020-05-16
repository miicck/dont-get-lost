using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class options_menu : MonoBehaviour
{
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
