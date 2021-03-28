using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skill : MonoBehaviour
{
    public int default_priority = 1;
    public bool is_visible = true;
    public bool possible_when_under_attack = false;
    public string noun_form;

    public string display_name => name.Replace("_", " ").ToLower().capitalize();

    public enum PRIORITY : byte
    {
        OFF = 0,
        LOW = 1,
        MED = 2,
        HIGH = 3
    };

    /// <summary> Returns the next priority in the cycle. </summary>
    public static PRIORITY cycle_priority(PRIORITY p)
    {
        switch (p)
        {
            case PRIORITY.OFF: return PRIORITY.LOW;
            case PRIORITY.LOW: return PRIORITY.MED;
            case PRIORITY.MED: return PRIORITY.HIGH;
            case PRIORITY.HIGH: return PRIORITY.OFF;
            default: throw new System.Exception("Unkown priority level: " + p);
        }
    }

    /// <summary> Returns a color for UI elements 
    /// reflecting the given priority. </summary>
    public static Color priority_color(PRIORITY p)
    {
        switch (p)
        {
            case PRIORITY.OFF: return new Color(0.5f, 0.5f, 0.5f);
            case PRIORITY.LOW: return new Color(1f, 0.7f, 0.7f);
            case PRIORITY.MED: return new Color(0.7f, 0.7f, 1f);
            case PRIORITY.HIGH: return new Color(0.7f, 1f, 0.7f);
            default: throw new System.Exception("Unkown priority level: " + p);
        }
    }

    /// <summary> Returns true with a probability 
    /// reflecting the given priority. </summary>
    public static bool priority_test(PRIORITY p)
    {
        switch (p)
        {
            case PRIORITY.OFF: return false;
            case PRIORITY.LOW: return Random.Range(0, 8) == 0;
            case PRIORITY.MED: return Random.Range(0, 4) == 0;
            case PRIORITY.HIGH: return Random.Range(0, 2) == 0;
            default: throw new System.Exception("Unkown priority level: " + p);
        }
    }

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
