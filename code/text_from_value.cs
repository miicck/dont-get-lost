using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class text_from_value : MonoBehaviour
{
    public void set(Transform set_from)
    {
        UnityEngine.UI.Text text = GetComponent<UnityEngine.UI.Text>();
        UnityEngine.UI.InputField input = GetComponent<UnityEngine.UI.InputField>();

        var slider = set_from.GetComponent<UnityEngine.UI.Slider>();
        if (slider != null)
        {
            if (text != null) text.text = "" + slider.value;
            if (input != null) input.text = "" + slider.value;
            return;
        }
    }
}