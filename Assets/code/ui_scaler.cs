using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.UI.CanvasScaler))]
public class ui_scaler : MonoBehaviour
{
    UnityEngine.UI.CanvasScaler scaler => GetComponent<UnityEngine.UI.CanvasScaler>(); 

    public float scale
    {
        get => scaler.referenceResolution.y / 1080f;
        set => scaler.referenceResolution = new Vector2(1920, 1080)*value;
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
                s.scale /= 1.1f;
            if (GUILayout.Button("Decrease scale"))
                s.scale *= 1.1f;
        }
    }
#endif

}
