using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crane_loader : MonoBehaviour, INonBlueprintable, INonEquipable
{
    public item_input input;
    public item_output output;
    public crane crane;

    item picked_up;

    private void Start()
    {
        // A little bit of startup time to let the crane initialize
        Invoke("drop_off", 0.5f);
    }

    void pick_up()
    {
        Debug.Log("pickup");
        if (input.item_count == 0)
        {
            // Nothing to input, try again later
            Invoke("pick_up", 0.1f);
            return;
        }
        else
        {    
            picked_up = input.release_item(0);
            picked_up.transform.SetParent(crane.hook);
            picked_up.transform.localPosition = Vector3.zero;

            crane.target = output.transform.position;
            crane.on_arrive = drop_off;
        }
    }

    void drop_off()
    {
        if (picked_up != null)
        {
            output.add_item(picked_up);
            picked_up = null;
        }

        crane.target = input.transform.position;
        crane.on_arrive = pick_up;
    }
}
