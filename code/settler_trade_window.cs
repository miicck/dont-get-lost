using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_trade_window : MonoBehaviour
{
    public RectTransform trades_go_here;

    public void setup(settler s)
    { 
        // Destroy any placeholder trades
        foreach (Transform t in trades_go_here)
            Destroy(t.gameObject);

        GetComponentInChildren<UnityEngine.UI.Text>().text = s.name.capitalize();

        // Setup the trade slots
        foreach (var t in s.GetComponentsInChildren<settler_trade>())
            t.trade_slot.transform.SetParent(trades_go_here);
    }

    public void on_open()
    {

    }
}
