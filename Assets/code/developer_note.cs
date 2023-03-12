using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class developer_note : MonoBehaviour
{
    public string note;

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(developer_note))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var note = (developer_note)target;
            note.note = GUILayout.TextArea(note.note);
        }
    }
#endif
}
