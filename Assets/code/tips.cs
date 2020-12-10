using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Attach this component to a text box so 
/// that it displays a new tip every time it's 
/// opened. Eventually these will include context-based as
/// well as general tips. </summary>
public class tips : MonoBehaviour
{
    UnityEngine.UI.Text text;

    private void Start()
    {
        text = GetComponent<UnityEngine.UI.Text>();
        set_tip();
    }

    private void OnEnable()
    {
        set_tip();
    }

    void set_tip()
    {
        if (text == null) return;
        if (general_tips == null) return;
        if (general_tips.Count == 0) return;

        // Set a random general tip
        text.text = "Tip: " + general_tips[Random.Range(0, general_tips.Count)];
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static List<string> general_tips = new List<string>();

    public static void add(string tip)
    {
        general_tips.Add(tip);
    }
}