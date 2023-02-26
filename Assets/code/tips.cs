using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Attach this component to a text box so 
/// that it displays the current context tip. </summary>
[RequireComponent(typeof(UnityEngine.UI.Text))]
public class tips : MonoBehaviour
{
    UnityEngine.UI.Text text => GetComponent<UnityEngine.UI.Text>();
    private void Start() => all_tips.Add(this);
    private void OnDestroy() => all_tips.Remove(this);

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<tips> all_tips = new HashSet<tips>();

    public static string context_tip
    {
        get => _context_tip;
        set
        {
            _context_tip = value;
            foreach (var t in all_tips)
                t.text.text = value;
        }
    }
    static string _context_tip;
}