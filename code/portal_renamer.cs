using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal_renamer : MonoBehaviour
{
    public UnityEngine.UI.InputField field;

    private void Start()
    {
        field.onValueChanged.AddListener((new_val) =>
        {
            var lm = player.current.left_menu;
            if (lm is portal)
            {
                var p = (portal)lm;
                field.text = p.attempt_rename(new_val);
            }
        });

        field.onEndEdit.AddListener((final_val) =>
        {
            // Refresh UI
            player.current.ui_state = player.UI_STATE.ALL_CLOSED;
            player.current.ui_state = player.UI_STATE.INVENTORY_OPEN;
        });
    }

    private void OnEnable()
    {
        var lm = player.current.left_menu;
        if (lm is portal)
        {
            var p = (portal)lm;
            field.text = p.teleport_name();
        }
        else field.text = "";
    }
}
