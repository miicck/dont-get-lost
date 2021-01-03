using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shop : settler_interactable, IAddsToInspectionText
{
    public settler_path_element cashier_spot;

    //##########//
    // FITTINGS //
    //##########//

    shop_type type_of_shop;
    shop_fitting fitting;
    item_dispenser materials_cupboard;

    bool fittings_exist()
    {
        if (type_of_shop == null) return false;
        if (materials_cupboard == null) return false;
        if (fitting == null) return false;
        return true;
    }

    bool validate_fittings()
    {
        if (fittings_exist())
            return true; // We've got a valid shop setup

        if (cashier_spot == null)
            Debug.LogError("Shop requires a cashier spot!");

        foreach (var e in settler_path_element.elements_in_room(cashier_spot.room))
        {
            if (type_of_shop == null)
            {
                // Figure out the type of shop
                var sf = e.GetComponentInParent<shop_fitting>();
                if (sf is shop_fitting)
                {
                    type_of_shop = shop_type.get_type(sf);
                    if (type_of_shop != null)
                        fitting = sf;
                }
            }

            if (materials_cupboard == null)
            {
                // Identify the materials dispenser
                var dispenser = e.GetComponentInParent<item_dispenser>();
                if (dispenser != null && dispenser.mode == item_dispenser.MODE.SHOP_MATERIALS_CUPBOARD)
                    materials_cupboard = dispenser;
            }
        }

        if (!fittings_exist())
        {
            // Failed to setup the shop properly, reset everything
            if (materials_cupboard != null)
                materials_cupboard.specific_material = null;
            return false;
        }

        // Shop setup successful, setup everything accordingly
        materials_cupboard.specific_material = Resources.Load<item>("items/" + type_of_shop.required_material());
        return true;
    }

    //##############//
    // INTERACTABLE //
    //##############//

    STAGE stage;
    item item_carrying;
    settler_path_element.path path;
    float stage_timer = 0;
    int cycles_completed = 0;

    public override string task_info() { return type_of_shop?.task_info(stage); }

    public override INTERACTION_RESULT on_assign(settler s)
    {
        // Starts in the stock stage
        stage = STAGE.STOCK;
        stage_timer = 0;
        cycles_completed = 0;
        path = null;
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override void on_unassign(settler s)
    {
        // Ensure we don't leave materials behind
        if (item_carrying != null)
            Destroy(item_carrying.gameObject);
    }

    public override INTERACTION_RESULT on_interact(settler s)
    {
        if (!validate_fittings())
            return INTERACTION_RESULT.FAILED;

        // No path, move to next stage
        if (path == null)
            complete_stage(s);
        else
        {
            // Walk the path
            if (path.walk(s.transform, s.walk_speed))
            {
                stage_timer += Time.deltaTime;
                if (stage_timer > 1f)
                {
                    stage_timer = 0f;
                    path = null;
                }
            }
        }

        if (stage == STAGE.GET_MATERIALS && !materials_cupboard.has_items_to_dispense)
            return INTERACTION_RESULT.FAILED;

        if (cycles_completed >= 4) return INTERACTION_RESULT.COMPLETE;
        return INTERACTION_RESULT.UNDERWAY;
    }

    enum STAGE
    {
        GET_MATERIALS,
        CRAFT,
        STOCK
    }

    void complete_stage(settler s)
    {
        switch (stage)
        {
            case STAGE.GET_MATERIALS:

                // Pickup the item
                item_carrying = materials_cupboard.dispense_first_item();
                if (item_carrying != null)
                {
                    // Put the item in hand
                    item_carrying.transform.SetParent(s.right_hand);
                    item_carrying.transform.localPosition = Vector3.zero;
                }

                // Go to the craft stage
                stage = STAGE.CRAFT;
                path = new settler_path_element.path(
                    materials_cupboard.path_element(s.group),
                    fitting.path_element(s.group)
                );

                break;

            case STAGE.CRAFT:
                // Delete the material
                if (item_carrying != null)
                    Destroy(item_carrying.gameObject);

                // Move to the stocking stage
                stage = STAGE.STOCK;
                path = new settler_path_element.path(
                    fitting.path_element(s.group),
                    path_element(s.group)
                );

                break;

            case STAGE.STOCK:
                ++cycles_completed;
                stage = STAGE.GET_MATERIALS;
                path = new settler_path_element.path(
                    path_element(s.group),
                    materials_cupboard.path_element(s.group)
                );
                break;

            default:
                throw new System.Exception("Unkown stage!");
        }
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        validate_fittings();
        if (type_of_shop == null) return "Shop is missing fittings.";
        return type_of_shop.inspection_text();
    }

    //############//
    // SHOP TYPES //
    //############//

    abstract class shop_type
    {
        public static shop_type get_type(shop_fitting crafter)
        {
            if (crafter.name == "sawmill")
                return new carpenter();
            return null;
        }

        public abstract string inspection_text();
        public abstract string required_material();
        public abstract string task_info(STAGE stage);
    }

    class carpenter : shop_type
    {
        public override string inspection_text()
        {
            return "This is a carpenters shop.";
        }

        public override string required_material()
        {
            return "log";
        }

        public override string task_info(STAGE stage)
        {
            switch(stage)
            {
                case STAGE.GET_MATERIALS:
                    return "Getting logs needed for carpentry.";
                case STAGE.CRAFT:
                    return "Carrying out carpentry.";
                case STAGE.STOCK:
                    return "Stocking carpenters shop";
                default:
                    throw new System.Exception("Unknown stage!");
            }
        }
    }
}
