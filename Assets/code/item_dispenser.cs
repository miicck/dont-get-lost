using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : settler_interactable
{
    public item_input input;
    public item_output overflow_output;

    public const float TIME_TO_DISPENSE = 1f;

    item_locator[] locators;
    float time_interacted = 0f;
    float time_dispensing = 0f;

    item_locator find_food()
    {
        foreach (var l in locators)
            if (l.item != null)
                if (l.item.food_values != null)
                    return l;
        return null;
    }

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
            if (locator == null || !accepts_item(itm))
            {
                // Reject item (give to overflow output if we have one)
                if (overflow_output == null)
                    Destroy(itm.gameObject);
                else
                    overflow_items.Add(itm);
                continue;
            }

            locator.item = itm;
        }

        foreach (var i in new List<item>(overflow_items))
        {
            if (utils.move_towards(i.transform, overflow_output.transform.position, Time.deltaTime))
            {
                overflow_output.add_item(i);
                overflow_items.Remove(i);
            }
        }
    }

    bool accepts_item(item i)
    {
        switch (mode)
        {
            case MODE.FOOD: return i.food_values != null;
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
        time_dispensing = 0f;
    }

    public override void on_interact(settler s)
    {
        time_interacted += Time.deltaTime;
        time_dispensing += Time.deltaTime;

        if (time_dispensing > TIME_TO_DISPENSE)
        {
            // Reset dispensing timer
            time_dispensing = 0;

            // Search for food
            var food = find_food();
            if (food != null)
            {
                // Eat food on authority client, delete food on all clients
                if (s.has_authority)
                {
                    // Eat food
                    s.nutrition.consume_food(food.item.food_values);
                }
                Destroy(food.release_item().gameObject);
            }
        }
    }

    public override bool is_complete(settler s)
    {
        switch (mode)
        {
            case MODE.FOOD:

                // Done if there is no food, or meetabolic satisfaction is high
                if (find_food() == null) return true;
                return s.nutrition.metabolic_satisfaction > 220; // Test for food eaten

            default:
                throw new System.Exception("Unkown item dispenser mode!");
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
