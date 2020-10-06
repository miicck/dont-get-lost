using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A simple track for items. This component ensures 
/// that the items roll downhill. It also records/remembers
/// the average item flow, so that it can reproduce it when
/// it's input isn't loaded. </summary>
public class item_gutter : item_logistic_building
{
    public const float ITEM_FLOW_TIMESPAN = 60f;
    public const float ITEM_SEPERATION = 0.25f;

    List<item> items = new List<item>();
    item_link_point output => item_outputs[0];

    /// <summary> Overloaded version of assign links, 
    /// sets the lowest link to the output and all others 
    /// to inputs. </summary>
    protected override void assign_links(
        out List<item_link_point> inputs,
        out List<item_link_point> outputs)
    {
        var links = GetComponentsInChildren<item_link_point>();
        if (links.Length < 2)
            throw new System.Exception("A gutter must have at least 2 links!");

        item_link_point output = null;

        // Set the lowest link point as the output, record the rest as inputs
        float min_alt = Mathf.Infinity;
        foreach (var lp in links)
            if (lp.position.y < min_alt)
            {
                min_alt = lp.position.y;
                output = lp;
            }

        output.type = item_link_point.TYPE.OUTPUT;

        // Record all the inputs
        inputs = new List<item_link_point>();
        foreach (var l in links)
            if (l != output)
            {
                l.type = item_link_point.TYPE.INPUT;
                inputs.Add(l);
            }

        outputs = new List<item_link_point> { output };
    }

    void OnDestroy()
    {
        // Destroy my items along with me
        foreach (var itm in items)
            if (itm != null)
                Destroy(itm.gameObject);
    }

    bool can_accept_input(item_link_point input)
    {
        return true;

        // No items on gutter => free
        if (items.Count == 0) return true;


        // See if there is enough space near this input
        var closest = utils.find_to_min(items, (i) => (i.transform.position - input.transform.position).magnitude);
        return (closest.transform.position - input.transform.position).magnitude > ITEM_SEPERATION;
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
        // Forget about any deleted items
        items.RemoveAll((i) => i == null);

        foreach (var input in item_inputs)
            if (input.item != null && can_accept_input(input))
            {
                // Take an item from the input
                // and place it on the gutter
                var to_add = input.release_item();
                items.Add(to_add);

                // Keep items so items[0] is the closest to the output
                items.Sort((a, b) =>
                {
                    float a_dis = (a.transform.position - output.transform.position).magnitude;
                    float b_dis = (b.transform.position - output.transform.position).magnitude;
                    return a_dis.CompareTo(b_dis);
                });

                to_add.transform.forward = output.position - input.position;
            }

        for (int i = 1; i < items.Count; ++i)
        {
            // Get direction towards next item
            Vector3 delta = items[i - 1].transform.position -
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
