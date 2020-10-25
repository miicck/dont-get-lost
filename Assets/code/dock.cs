using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dock : MonoBehaviour
{
    public item_input boat_input;
    public boat boat;

    private void Start()
    {
        boat_input.add_on_change_listener(on_input_change);
    }

    void on_input_change()
    {
        foreach (var i in boat_input.relesae_all_items())
            boat.add_item(i);
    }
}
