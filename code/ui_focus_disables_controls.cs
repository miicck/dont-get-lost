using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ui_focus_disables_controls : MonoBehaviour
{
    public UnityEngine.UI.InputField input_field;
    public static bool controls_disabled;

    bool disable()
    {
        if (input_field != null)
            if (input_field.isFocused)
                return true;

        return false;
    }

    public void Update()
    {
        controls_disabled = disable();
    }
}
