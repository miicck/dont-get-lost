using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shop : walk_to_settler_interactable, 
    IAddsToInspectionText, IPlayerInteractable, IBuildListener
{
    public town_path_element cashier_spot;

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_string_counts stock;
    delegate void stock_listener(int new_count);
    Dictionary<string, stock_listener> stock_listeners;

    void add_stock_change_listener(string s, stock_listener sl)
    {
        if (stock_listeners.TryGetValue(s, out stock_listener prev))
            stock_listeners[s] = prev + sl;
        else
            stock_listeners[s] = sl;
    }

    void invoke_stock_listeners()
    {
        foreach (var kv in stock_listeners)
            kv.Value(stock[kv.Key]);
    }

    public override IExtendsNetworked.callbacks get_callbacks()
    {
        var ret = base.get_callbacks();
        ret.init_networked_variables += () =>
        {
            stock = new networked_variables.net_string_counts();
            stock_listeners = new Dictionary<string, stock_listener>();
            stock.on_change = invoke_stock_listeners;
        };
        return ret;
    }

    //################//
    // IBuildListener //
    //################//

    bool need_stock_reset = false;
    public void on_first_built()
    {
        need_stock_reset = true;
    }

    //##########//
    // FITTINGS //
    //##########//

    shop_type type_of_shop
    {
        get => _type_of_shop;
        set
        {
            _type_of_shop = value;
            if (_type_of_shop == null) skill = null;
            else skill = _type_of_shop.relevant_skill();
        }
    }
    shop_type _type_of_shop;


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

        foreach (var e in town_path_element.elements_in_room(cashier_spot.room))
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
    town_path_element.path path;
    float stage_work_done = 0;
    int stock_crafted = 0;
    int left_to_stock = 0;

    public override string task_info() { return type_of_shop?.task_info(stage); }

    protected override void on_arrive(settler s)
    {
        // Starts in the stock stage
        stage = STAGE.STOCK;
        stage_work_done = 0;
        stock_crafted = 0;
        left_to_stock = 0;
        path = null;
    }

    protected override void on_unassign(settler s)
    {
        // Ensure we don't leave materials behind
        if (item_carrying != null)
            Destroy(item_carrying.gameObject);
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        if (!validate_fittings())
            return STAGE_RESULT.TASK_FAILED;

        // No path, move to next stage
        if (path == null)
            complete_stage(s);
        else
        {
            // Walk the path
            if (path.walk(s.transform, s.walk_speed, s) == null)
            {
                stage_work_done += Time.deltaTime * s.skills[skill].speed_multiplier;
                if (stage_work_done > 1f)
                {
                    stage_work_done = 0f;
                    path = null;
                }
            }
        }

        if (this.stage == STAGE.GET_MATERIALS && !materials_cupboard.has_items_to_dispense)
            return STAGE_RESULT.TASK_FAILED;

        if (stock_crafted >= 4) return STAGE_RESULT.TASK_COMPLETE;
        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    public enum STAGE
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
                path = new town_path_element.path(
                    materials_cupboard.path_element(s.group),
                    fitting.path_element(s.group)
                );

                break;

            case STAGE.CRAFT:
                // Delete the material
                if (item_carrying != null)
                {
                    Destroy(item_carrying.gameObject);
                    ++stock_crafted;
                    ++left_to_stock;
                }

                // Move to the stocking stage
                stage = STAGE.STOCK;
                path = new town_path_element.path(
                    fitting.path_element(s.group),
                    path_element(s.group)
                );

                break;

            case STAGE.STOCK:
                stage = STAGE.GET_MATERIALS;
                path = new town_path_element.path(
                    path_element(s.group),
                    materials_cupboard.path_element(s.group)
                );

                while (left_to_stock > 0)
                {
                    // Increment the first sold item not in full stock
                    --left_to_stock;
                    foreach (var item_name in type_of_shop.items_sold())
                    {
                        if (stock[item_name] >= 10) continue;
                        stock[item_name] += 1;
                        break;
                    }

                    // Decrement the first bought item with stock
                    foreach (var item_name in type_of_shop.items_bought())
                    {
                        if (stock[item_name] > 0)
                        {
                            stock[item_name] -= 1;
                            break;
                        }
                    }
                }

                break;

            default:
                throw new System.Exception("Unkown stage!");
        }
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        validate_fittings();
        return base.added_inspection_text() + "\n" +
            ((type_of_shop == null) ? "Shop is missing fittings." :
            type_of_shop.inspection_text());
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public player_interaction[] player_interactions()
    {
        if (interactions == null)
            interactions = new player_interaction[] { new shop_interaction(this) };
        return interactions;
    }

    class shop_interaction : left_player_menu
    {
        shop shop;
        public shop_interaction(shop shop) : base("shop") { this.shop = shop; }

        public override bool is_possible()
        {
            if (shop == null) return false;
            shop.validate_fittings();
            if (shop.type_of_shop == null) return false;
            return true;
        }

        protected override RectTransform create_menu()
        {
            return Resources.Load<RectTransform>("ui/shop_menu").inst();
        }

        protected override void on_open()
        {
            if (shop.need_stock_reset)
            {
                shop.need_stock_reset = false;

                // Start with no stock of sold items
                foreach (var item_name in shop.type_of_shop.items_sold())
                    if (shop.stock[item_name] != 0)
                        shop.stock[item_name] = 0;

                // Start with full stock of bought items (so player can't 
                // just delete/remake the shop to sell more stuff).
                foreach (var item_name in shop.type_of_shop.items_bought())
                    if (shop.stock[item_name] < 10)
                        shop.stock[item_name] = 10;
            }

            // Update options
            RectTransform sell_content = null;
            RectTransform buy_content = null;
            foreach (var rt in menu.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
            {
                var content = rt.content;
                if (content.name.Contains("sell")) sell_content = content;
                else buy_content = content;
            }

            // Clear previous options
            foreach (RectTransform child in sell_content) Destroy(child.gameObject);
            foreach (RectTransform child in buy_content) Destroy(child.gameObject);

            var template = Resources.Load<RectTransform>("ui/shop_option");

            // Create the buy options
            foreach (var item_name in shop.type_of_shop.items_sold())
            {
                var itm = Resources.Load<item>("items/" + item_name);
                if (itm == null)
                {
                    Debug.LogError("Unkown item in shop: " + item_name);
                    continue;
                }

                var option = template.inst();
                option.transform.SetParent(buy_content);

                var sprite = option.get_child_with_name<UnityEngine.UI.Image>("sprite");
                sprite.sprite = itm.sprite;

                var text = option.get_child_with_name<UnityEngine.UI.Text>("text");
                text.text = itm.display_name;

                var price_text = option.get_child_with_name<UnityEngine.UI.Text>("price_text");
                int price = Mathf.Max(1, itm.value);
                price_text.text = price.qs();

                var but = option.GetComponentInChildren<UnityEngine.UI.Button>();
                but.onClick.AddListener(() =>
                {
                    int count = controls.held(controls.BIND.CRAFT_FIVE) ? 5 : 1;
                    count = Mathf.Min(count, shop.stock[item_name]);
                    if (count == 0)
                        popup_message.create(itm.plural + " are out of stock!");
                    else if (player.current.inventory.remove("coin", price * count))
                    {
                        player.current.inventory.add(item_name, count);
                        shop.stock[item_name] = Mathf.Max(0, shop.stock[item_name] - count);
                    }
                    else if (count > 1)
                        popup_message.create("You can't afford " + count + " " + itm.plural + "!");
                    else
                        popup_message.create("You can't afford any " + itm.plural + "!");

                });

                shop.add_stock_change_listener(item_name, (count) =>
                {
                    if (text == null) return;
                    text.text = Resources.Load<item>("items/" + item_name).display_name + " (" + count.qs() + ")";
                });
            }

            // Create the sell options
            foreach (var item_name in shop.type_of_shop.items_bought())
            {
                var itm = Resources.Load<item>("items/" + item_name);
                if (itm == null)
                {
                    Debug.LogError("Unkown item in shop: " + item_name);
                    continue;
                }

                var option = template.inst();
                option.transform.SetParent(sell_content);

                var sprite = option.get_child_with_name<UnityEngine.UI.Image>("sprite");
                sprite.sprite = itm.sprite;

                var text = option.get_child_with_name<UnityEngine.UI.Text>("text");
                text.text = itm.display_name;

                var price_text = option.get_child_with_name<UnityEngine.UI.Text>("price_text");
                int price = Mathf.Max(1, itm.value);
                price_text.text = price.qs();

                var but = option.GetComponentInChildren<UnityEngine.UI.Button>();
                but.onClick.AddListener(() =>
                {
                    int count = controls.held(controls.BIND.CRAFT_FIVE) ? 5 : 1;
                    int stock = shop.stock[item_name];

                    // Check the shop still wants to buy this
                    if (stock >= 10)
                    {
                        popup_message.create("The shop will not buy any more " + itm.plural + "!");
                        return;
                    }

                    // Don't let player overstock the shop
                    count = Mathf.Min(count, 10 - stock);

                    // Check the player has this
                    int in_inv = player.current.inventory.count(item_name);
                    if (in_inv < count)
                    {
                        if (count < 2)
                            popup_message.create("You do not have " + utils.a_or_an(itm.name) + " " + itm.name + " to sell!");
                        else
                            popup_message.create("You do not have " + count + " " + itm.plural + " to sell!");
                        return;
                    }

                    // Sell to the shop
                    if (player.current.inventory.remove(item_name, count))
                    {
                        player.current.inventory.add("coin", count * price);
                        shop.stock[item_name] = shop.stock[item_name] + count;
                    }
                    else Debug.LogError("Failed to remove sold items from player inventory!");

                });

                shop.add_stock_change_listener(item_name, (count) =>
                {
                    if (text == null) return;
                    text.text = Resources.Load<item>("items/" + item_name).display_name + " (" + count.qs() + ")";
                });
            }


            shop.invoke_stock_listeners();
        }
    }

    //############//
    // SHOP TYPES //
    //############//

    public abstract class shop_type
    {
        public static shop_type get_type(shop_fitting crafter)
        {
            if (crafter.name == "sawmill")
                return new carpenter();
            if (crafter.name == "furnace")
                return new blacksmith();
            return null;
        }

        public abstract skill relevant_skill();
        public abstract string shop_name();
        public abstract string[] items_sold();
        public abstract string[] items_bought();
        public abstract string inspection_text();
        public abstract string required_material();
        public abstract string task_info(STAGE stage);
    }

    public class carpenter : shop_type
    {
        public override skill relevant_skill()
        {
            return Resources.Load<skill>("skills/carpentry");
        }

        public override string shop_name()
        {
            return "Carpenter's shop";
        }

        public override string inspection_text()
        {
            return "This is a carpenter's shop.";
        }

        public override string required_material()
        {
            return "log";
        }

        public override string[] items_sold()
        {
            return new string[]
            {
                "plank",
                "reinforced_wood_panel",
                "window_panel"
            };
        }

        public override string[] items_bought()
        {
            return new string[]
            {
                "log"
            };
        }

        public override string task_info(STAGE stage)
        {
            switch (stage)
            {
                case STAGE.GET_MATERIALS:
                    return "Getting logs needed for carpentry.";
                case STAGE.CRAFT:
                    return "Carrying out carpentry.";
                case STAGE.STOCK:
                    return "Stocking carpenters shop";
                default:
                    return "Carpentry";
            }
        }
    }

    public class blacksmith : shop_type
    {
        public override skill relevant_skill()
        {
            return Resources.Load<skill>("skills/smithing");
        }

        public override string shop_name()
        {
            return "Blacksmith's shop";
        }

        public override string inspection_text()
        {
            return "This is a Blacksmith's shop";
        }

        public override string required_material()
        {
            return "coal";
        }

        public override string[] items_sold()
        {
            return new string[]
            {
                "iron",
                "iron_sword",
                "iron_axe",
                "iron_pickaxe"
            };
        }

        public override string[] items_bought()
        {
            return new string[]
            {
                "coal",
                "iron_ore",
            };
        }

        public override string task_info(STAGE stage)
        {
            switch (stage)
            {
                case STAGE.GET_MATERIALS:
                    return "Fueling the furnace.";
                case STAGE.CRAFT:
                    return "Smithing.";
                case STAGE.STOCK:
                    return "Stocking the blacksmith's shop.";
                default:
                    return "Smithing";
            }
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static List<shop_type> all_shop_types()
    {
        return new List<shop_type> { new carpenter() };
    }
}
