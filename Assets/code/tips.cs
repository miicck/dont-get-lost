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

    public enum MODE
    {
        GENERAL,
        CONTEXT,
    }
    public MODE mode;

    private void Start()
    {
        text = GetComponent<UnityEngine.UI.Text>();

        switch (mode)
        {
            case MODE.GENERAL:
                set_general_tip();
                break;

            case MODE.CONTEXT:
                text.text = "";
                break;

            default:
                throw new System.Exception("Unkown tip mode!");
        }

        all_tips.Add(this);
    }

    private void OnEnable()
    {
        if (text == null) return;

        switch (mode)
        {
            case MODE.GENERAL:
                set_general_tip();
                break;

            case MODE.CONTEXT:
                text.text = "";
                break;

            default:
                throw new System.Exception("Unkown tip mode!");
        }
    }

    private void OnDestroy()
    {
        all_tips.Remove(this);
    }

    void set_general_tip()
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
    static HashSet<tips> all_tips = new HashSet<tips>();

    public static void add(string tip)
    {
        general_tips.Add(tip);
    }

    public static string context_tip
    {
        get => _context_tip;
        set
        {
            _context_tip = value;
            foreach (var t in all_tips)
                if (t.mode == MODE.CONTEXT)
                    t.text.text = value;
        }
    }
    static string _context_tip;
}