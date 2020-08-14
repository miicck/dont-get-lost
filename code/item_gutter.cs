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

    void OnDestroy()
    {
        // Destroy my items along with me
        foreach(var itm in items)
            if (itm != null)
                Destroy(itm.gameObject);
    }

    bool can_accept_input()
    {
        // No items on gutter => free
        if (items.Count == 0) return true;

        // See if there is enough space at the start of the gutter
        Vector3 delta = items[items.Count-1].transform.position - input.position;
        return delta.magnitude > ITEM_SEPERATION;
    }

    bool can_output()
    {
        // Can't output if output is already occupied
        if (output.item != null) return false;

        // Can assign to output if it is a dead end
        if (output.linked_to == null) return true; 

        // Can't assign to output if the output's output
        // isn't free (as it is likely less than ITEM_SEPERATION away)
        if (output.linked_to.item != null) return false;

        // g2g
        return true;
    }

    private void Update()
    {
        if (input.item != null)
        {
            // Take an item from the input
            // and place it on the gutter
            if (can_accept_input())
                items.Add(input.release_item());
        }

        for (int i=1; i<items.Count; ++i)
        {
            // Get direction towards next item
            Vector3 delta = items[i-1].transform.position -
                            items[i].transform.position;

            // Only move towards the next
            // item if we're far enough apart
            if (delta.magnitude > ITEM_SEPERATION)
            {
                // Move up to ITEM_SEPERATION away from the next item
                delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                float max_move = Time.deltaTime;
                if (delta.magnitude > max_move)
                    delta = delta.normalized * max_move;
                items[i].transform.position += delta;
            }
        }

        if (items.Count > 0)
        {
            // Move the item nearest the output
            if (can_output())
            {
                // Move first item towards (unoccupied) output
                if (utils.move_towards(items[0].transform, 
                    output.position, Time.deltaTime))
                {
                    output.item = items[0];
                    items.RemoveAt(0);
                }
            }
            else
            {
                // Move up to ITEM_SEPERATION away from (occupied) output
                Vector3 delta = output.position - items[0].transform.position;
                if (delta.magnitude > ITEM_SEPERATION)
                {
                    delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                    float max_move = Time.deltaTime;
                    if (delta.magnitude > max_move)
                        delta = delta.normalized * max_move;
                    items[0].transform.position += delta;
                }
            }
        }
    }
}
