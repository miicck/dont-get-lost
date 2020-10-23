using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : settler_interactable
{
    public item_input input;
    item_locator[] locators;

    public enum MODE
    {
        FOOD
    }
    public MODE mode;

    protected override void Start()
    {
        if (input == null)
            throw new System.Exception("Item dispenser has no input!");

        locators = GetComponentsInChildren<item_locator>();
        if (locators.Length == 0)
            throw new System.Exception("Item dispenser has no locators!");

        base.Start();
    }

    private void Update()
    {
        // Search for an available locator
        foreach (var l in locators)
        {
            // This locator isn't free
            if (l.item != null) continue;

            // Get the first usable item, discarding
            // the useless ones
            item itm = null;
            while (itm == null && input.item_count > 0)
            {
                itm = input.release_item(0);
                if (accepts_item(itm))
                    break; // Accept

                // Reject
                Destroy(itm.gameObject);
                itm = null;
            }

            if (itm == null)
                break; // No acceptable items

            l.item = itm;
        }
    }

    public override bool interact(settler s, float time_elapsed)
    {
        switch (mode)
        {
            case MODE.FOOD:

                // It takes 1 second to eat food
                if (time_elapsed < 1f)
                    return false;

                // Search for food
                foreach (var l in locators)
                    if (l.item != null)
                    {
                        s.hunger -= l.item.food_value;
                        Destroy(l.release_item().gameObject);
                        break;
                    }

                return true;

            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }

    bool accepts_item(item i)
    {
        switch (mode)
        {
            case MODE.FOOD: return i.food_value > 0;
            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }
}
