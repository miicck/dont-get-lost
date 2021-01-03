using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : settler_interactable, IAddsToInspectionText
{
    public item_input input;
    public item_output overflow_output;
    public item specific_material;

    public const float TIME_TO_DISPENSE = 1f;

    item_locator[] locators;
    float time_interacted = 0f;
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
        FOOD,
        SHOP_MATERIALS_CUPBOARD
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
            if (utils.move_towards_and_look(i.transform,
                overflow_output.transform.position,
                Time.deltaTime, level_look: false))
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
            case MODE.SHOP_MATERIALS_CUPBOARD: return i.name == specific_material?.name;
            default: throw new System.Exception("Unkown dispenser mode!");
        }
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        switch (mode)
        {
            case MODE.SHOP_MATERIALS_CUPBOARD:
                if (specific_material == null) return "Not providing anything to shop.";
                else return "Providing " + specific_material.plural + " to the shop.";
        }
        return null;
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

            switch (mode)
            {
                case MODE.FOOD:
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
                    break;

                default:
                    throw new System.Exception("Unkown item dispenser mode!");
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
                return s.nutrition.metabolic_satisfaction > settler.MAX_METABOLIC_SATISFACTION_TO_EAT;

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
