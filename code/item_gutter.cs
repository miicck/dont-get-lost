using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A simple track for items. This component ensures 
/// that the items roll downhill. </summary>
public class item_gutter : item_proccessor
{
    public const float ITEM_SEPERATION = 0.25f;

    item_link_point input;
    item_link_point output;

    List<item> items = new List<item>();

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

    bool can_accept_input()
    {
        // No items on gutter => free
        if (items.Count == 0) return true;

        // See if there is enough space at the start of the gutter
        Vector3 delta = items[items.Count-1].transform.position - input.position;
        return delta.magnitude > ITEM_SEPERATION;
    }

    private void Update()
    {
        if (output.item != null)
            return; // Output blocked up

        if (input.item != null)
        {
            // Take an item from the input
            // and place it on the gutter
            if (can_accept_input())
                items.Add(input.release_item());
        }

        item to_output = null;
        foreach (var itm in items)
        {
            // Move the items along the track
            Vector3 delta = output.position - itm.transform.position;
            float max_move = Time.deltaTime;
            if (delta.magnitude > max_move)
                delta = delta.normalized * max_move;
            else
                // Item has reached output
                to_output = itm;

            itm.transform.position += delta;
        }

        if (to_output != null)
        {
            items.Remove(to_output);
            output.item = to_output;
        }

        /*
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
        */
    }
}
