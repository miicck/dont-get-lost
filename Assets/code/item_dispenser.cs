using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : settler_interactable
{
    public item_input input;
    item_locator[] locators;
    float time_interacted = 0f;

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

    bool accepts_item(item i)
    {
        switch (mode)
        {
            case MODE.FOOD: return i.food_value > 0;
            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }

    //##############//
    // INTERACTABLE //
    //##############//

    public override void on_assign(settler s)
    {
        // Reset stuff
        time_interacted = 0f;
    }

    public override void on_interact(settler s)
    {
        time_interacted += Time.deltaTime;
    }

    public override bool is_complete(settler s)
    {
        // It takes 1 second to dispense item
        return time_interacted > 1f;
    }

    public override void on_unassign(settler s)
    {
        // Only dispense item on authority client
        if (!s.has_authority) return;

        // Dispense the item
        switch (mode)
        {
            case MODE.FOOD:

                // Search for food
                foreach (var l in locators)
                    if (l.item != null)
                    {
                        if (l.item.food_value > s.hunger.value)
                            break;

                        s.hunger.value -= l.item.food_value;
                        Destroy(l.release_item().gameObject);
                    }

                break;

            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }

    public override string task_info()
    {
        switch (mode)
        {
            case MODE.FOOD: return "Getting food";
            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }
}
