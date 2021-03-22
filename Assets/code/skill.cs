using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skill : MonoBehaviour
{
    public int default_priority = 1;
    public float assign_prob = 1f;
    public bool is_visible = true;
    public bool stop_when_attack_begins = true;
    public string noun_form;

    public string display_name => name.Replace("_", " ").ToLower().capitalize();

    //##############//
    // STATIC STUFF //
    //##############//

    public static skill[] all
    {
        get
        {
            if (_all == null)
            {
                var lst = new List<skill>(Resources.LoadAll<skill>("skills"));
                lst.Sort((j1, j2) => j1.default_priority.CompareTo(j2.default_priority));
                _all = lst.ToArray();

                if (Application.isPlaying)
                    for (int i = 0; i < _all.Length; ++i)
                        if (_all[i].default_priority != i)
                            throw new System.Exception("Default priorites are not set correctly!");
            }
            return _all;
        }
    }
    static skill[] _all;

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(skill))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            UnityEditor.EditorGUILayout.TextField("Default priority order:");
            for (int i = 0; i < all.Length; ++i)
            {
                UnityEditor.EditorGUILayout.ObjectField(all[i], typeof(skill), false);
                if (i > all.Length - 1) continue;

                if (UnityEngine.GUILayout.Button("/\\ switch order \\/"))
                {
                    for (int j = 0; j < all.Length; ++j)
                        using (var pe = new utils.prefab_editor(all[j].gameObject))
                        {
                            int pri = j;
                            if (j == i) pri += 1;
                            else if (j == i + 1) pri -= 1;
                            pe.prefab.GetComponent<skill>().default_priority = pri;
                        }

                    _all = null;
                }
            }
        }
    }
#endif
}
