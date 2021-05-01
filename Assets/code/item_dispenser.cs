using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : MonoBehaviour
{
    public item_input input;
    public item_output overflow_output;

    public delegate bool item_accept_func(item i);
    public item_accept_func accept_item = (i) => true;

    item_locator[] locators;
    float time_dispensing = 0f;

    public bool has_items_to_dispense
    {
        get
        {
            foreach (var l in locators)
                if (l.item != null)
                    return true;
            return false;
        }
    }

    public item dispense_first_item()
    {
        foreach (var l in locators)
            if (l.item != null)
                return l.release_item();
        return null;
    }

    void Start()
    {
        if (input == null)
            throw new System.Exception("Item dispenser has no input!");

        locators = GetComponentsInChildren<item_locator>();
        if (locators.Length == 0)
            throw new System.Exception("Item dispenser has no locators!");
    }

    HashSet<item> overflow_items = new HashSet<item>();

    private void Update()
    {
        while (input.item_count > 0)
        {
            var itm = input.release_item(0);

            // Find an available locator
            item_locator locator = null;
            foreach (var l in locators)
                if (l.item == null)
                {
                    locator = l;
                    break;
                }

            // Reject unacceptable items, or if there are no outputs
            if (locator == null || !accept_item(itm))
            {
                // Reject item (give to overflow output if we have one)
                if (overflow_output == null)
                    Destroy(itm.gameObject);
                else
                {
                    Vector3 delta = overflow_output.transform.position - itm.transform.position;
                    delta.x = 0; delta.z = 0;
                    itm.transform.position += delta;
                    overflow_items.Add(itm);
                }
                continue;
            }

            locator.item = itm;
        }

        foreach (var i in new List<item>(overflow_items))
        {
            if (utils.move_towards_and_look(i.transform,
                overflow_output.transform.position,
                Time.deltaTime, level_look: false))
            {
                overflow_output.add_item(i);
                overflow_items.Remove(i);
            }
        }
    }
}
