using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal_renamer : MonoBehaviour
{
    UnityEngine.UI.InputField field;

    private void Start()
    {
        field = GetComponent<UnityEngine.UI.InputField>();
        if (field == null)
            throw new System.Exception("No input field on portal renamer!");

        field.onValueChanged.AddListener((new_val) =>
        {
            var lm = player.current.left_menu;
            if (lm is portal)
            {
                var p = (portal)lm;
                field.text = p.attempt_rename(new_val);
            }
            else throw new System.Exception("Could not find the portal to rename!");
        });
    }
}
