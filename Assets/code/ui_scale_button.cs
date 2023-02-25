using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ui_scale_button : MonoBehaviour
{
    public bool increase = true;

    private void Start()
    {
        GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
        {
            var scaler = FindObjectOfType<ui_scaler>();
            if (scaler == null) return;
            if (increase) scaler.scale *= 1.1f;
            else scaler.scale /= 1.1f;
        });
    }
}
