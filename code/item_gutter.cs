using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A simple track for items. This component ensures 
/// that the items roll downhill. </summary>
public class item_gutter : item_proccessor
{
    item_link_point input;
    item_link_point output;

    void Start()
    {
        item_link_point[] links = GetComponentsInChildren<item_link_point>();
        if (links.Length != 2)
            throw new System.Exception("Item gutters must have exactly 2 link points!");

        // The lower end is the output, the higher end is the input
        if (links[0].position.y < links[1].position.y)
        {
            links[0].type = item_link_point.TYPE.OUTPUT;
            output = links[0];

            links[1].type = item_link_point.TYPE.INPUT;
            input = links[1];
        }
        else
        {
            links[0].type = item_link_point.TYPE.INPUT;
            input = links[0];

            links[1].type = item_link_point.TYPE.OUTPUT;
            output = links[1];
        }
    }

    private void Update()
    {
        if (input.item == null)
            return; // Nothing to do

        // Move item along gutter
        Vector3 delta = output.position - input.item.transform.position;

        bool arrived = false;
        float max_move = Time.deltaTime;
        if (delta.magnitude > max_move)
            delta = delta.normalized * max_move;
        else arrived = true;

        input.item.transform.position += delta;

        // Transfer item to output link
        if (arrived)
            if (output.item == null)
                input.transfer_item(output);
    }
}
