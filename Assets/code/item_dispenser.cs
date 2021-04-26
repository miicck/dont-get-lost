using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_dispenser : walk_to_settler_interactable, IAddsToInspectionText
{
    public item_input input;
    public item_output overflow_output;
    public item specific_material;

    public const float TIME_TO_DISPENSE = 1f;

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

    public override string added_inspection_text()
    {
        switch (mode)
        {
            case MODE.SHOP_MATERIALS_CUPBOARD:
                return base.added_inspection_text() + "\n" +
                    ((specific_material == null) ?
                    "Not providing anything to shop." :
                    "Providing " + specific_material.plural + " to the shop.");
        }
        return base.added_inspection_text();
    }

    //##############//
    // INTERACTABLE //
    //##############//

    protected override bool ready_to_assign(settler s)
    {
        switch (mode)
        {
            case MODE.FOOD:
                if (s.nutrition.metabolic_satisfaction > settler.MAX_METABOLIC_SATISFACTION_TO_EAT)
                    return false;
                return find_food() != null;
            default: return false;
        }
    }

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        time_dispensing = 0f;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Run dispenser timer
        time_dispensing += Time.deltaTime;
        if (time_dispensing < TIME_TO_DISPENSE) 
            return STAGE_RESULT.STAGE_UNDERWAY;
        time_dispensing = 0f;

        // Item dispense possible
        switch (mode)
        {
            case MODE.FOOD:

                // Search for food
                var food = find_food();
                if (food == null) return STAGE_RESULT.TASK_FAILED;

                // Eat food on authority client, delete food on all clients
                if (s.has_authority) s.nutrition.consume_food(food.item.food_values);
                Destroy(food.release_item().gameObject);

                // Complete if we've eaten enough
                if (s.nutrition.metabolic_satisfaction > settler.MAX_METABOLIC_SATISFACTION_TO_EAT)
                    return STAGE_RESULT.TASK_COMPLETE;
                else
                    return STAGE_RESULT.STAGE_UNDERWAY;

            default:
                Debug.LogError("Unkown item dispenser mode!");
                return STAGE_RESULT.TASK_FAILED;
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
