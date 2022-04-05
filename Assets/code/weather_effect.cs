using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weather_effect : MonoBehaviour
{
    public float weight
    {
        get => _weight;
        set
        {
            _weight = value;
        }
    }
    float _weight = 1f;

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(weather_effect))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var we = (weather_effect)target;
            UnityEditor.EditorGUILayout.FloatField("Weight", we.weight);
            base.OnInspectorGUI();
        }
    }
#endif
}
