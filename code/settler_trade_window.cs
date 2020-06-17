using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_trade_window : MonoBehaviour
{
    public RectTransform trades_go_here;

    public void setup(string label, settler_trade[] trades)
    { 
        GetComponentInChildren<UnityEngine.UI.Text>().text = label;

        // Setup the trade slots
        foreach (var t in trades)
            t.slot.transform.SetParent(trades_go_here);
    }
}