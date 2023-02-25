using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.UI.CanvasScaler))]
public class ui_scaler : MonoBehaviour
{
    UnityEngine.UI.CanvasScaler scaler => GetComponent<UnityEngine.UI.CanvasScaler>(); 

    void set_scaler_scale(float scale)
    {
        scaler.referenceResolution = new Vector2(1920, 1080) / scale;
    }

    private void Start()
    {
        set_scaler_scale(scale);
    }

    public float scale
    {
        get
        {
            var val = PlayerPrefs.GetFloat("ui_scale", 1.0f);
            set_scaler_scale(val);
            return val;
        }
        set
        {
            PlayerPrefs.SetFloat("ui_scale", value);
            set_scaler_scale(value);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ui_scaler))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var s = (ui_scaler)target;
            GUILayout.TextArea("Current scale = "+s.scale);
            if (GUILayout.Button("Increase scale"))
                s.scale *= 1.1f;
            if (GUILayout.Button("Decrease scale"))
                s.scale /= 1.1f;
        }
    }
#endif

}
