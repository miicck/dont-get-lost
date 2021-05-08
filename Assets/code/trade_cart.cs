using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class has_trade_hub : MonoBehaviour, IExtendsNetworked
{
    //###################//
    // IExtendsNetworked //
    //###################//

    public trade_hub trade_hub
    {
        get
        {
            if (trade_hub_id.value == 0) return null;
            var th = networked.try_find_by_id(trade_hub_id.value, false);
            if (th is trade_hub) return (trade_hub)th;
            return null;
        }
    }

    networked_variables.net_int trade_hub_id;

    public IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                trade_hub_id = new networked_variables.net_int();
            },

            on_forget = (deleted) =>
            {
                // If I've been deleted, also delete my trade hub
                if (deleted) trade_hub?.delete();
            },

            on_auth_change = (auth) =>
            {
                if (!auth) return; // Not authority
                if (trade_hub != null) return; // Already have a trade hub

                // Create my trade hub/remember it's network id
                var th = (trade_hub)client.create(transform.position, "misc/trade_hub");
                th.on_register = () =>
                {
                    // Link me/my trade hub together
                    trade_hub_id.value = th.network_id;
                    th.owner = this;
                };
            }
        };
    }

    public virtual void add_item(item i) { }
}

public class trade_cart : has_trade_hub, IAddsToInspectionText, IPlayerInteractable
{
    public item_input input;
    public Transform input_drop;
    public item_output output;
    public Transform output_origin;

    item shipping;
    int rate_per_60sec = 0;
    float raw_rate = 0;
    bool limited_by_production = false;
    const float RATE_AV_WINDOW = 60f;

    private void Start()
    {
        InvokeRepeating("eval_rate", 1, 1);
    }

    private void Update()
    {
        // Move items from output origin to the output
        foreach (Transform t in output_origin)
            if (utils.move_towards(t, output.output_point, Time.deltaTime, allign_forwards: true))
            {
                var itm = t.GetComponent<item>();
                output.add_item(itm);
            }

        // Get items from the input and add them to the drop
        float rate_this_frame = 0f;
        foreach (var i in input.relesae_all_items())
        {
            if (shipping?.name != i.name)
            {
                shipping = Resources.Load<item>("items/" + i.name);
                raw_rate = 0f;
                rate_this_frame = 0f;
            }
            i.transform.SetParent(input_drop);
            rate_this_frame += 1f;
        }

        // Construct a rolling average of the raw input rate
        rate_this_frame /= Time.deltaTime;
        float x = Time.deltaTime / RATE_AV_WINDOW;
        raw_rate = raw_rate * (1 - x) + rate_this_frame * x;

        // Move items in the drop towards the drop-point
        foreach (Transform t in input_drop)
            if (utils.move_towards(t, input_drop.position, Time.deltaTime, allign_forwards: true))
            {
                var itm = t.GetComponent<item>();
                item_dropper.create(itm);
            }
    }

    void eval_rate()
    {
        // Not shipping anything
        if (shipping == null)
        {
            rate_per_60sec = 0;
            return;
        }

        // Get full town production of the shipped material
        rate_per_60sec = 0;
        var prod = production_tracker.current_production();
        foreach (var kv in prod)
            if (kv.Key.name == shipping.name)
                rate_per_60sec += kv.Value.rate_per_min_60sec_av;

        // Rate shipping is the minimum of the rate provided to this
        // cart (raw_rate*60) and the production of the whole town.
        int raw_rate_per60sec = (int)(raw_rate * 60f);

        if (rate_per_60sec < raw_rate_per60sec)
        {
            limited_by_production = true;
        }
        else
        {
            limited_by_production = false;
            rate_per_60sec = raw_rate_per60sec;
        }

        trade_hub?.set_item_rate(shipping, rate_per_60sec);
    }

    public override void add_item(item i)
    {
        // Create an item at the output origin
        var itm = item.create(i.name, output_origin.position, output_origin.rotation, logistics_version: true);
        itm.transform.SetParent(output_origin);
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        string ss = "Not shipping anything";
        if (shipping != null)
        {
            ss = "Shipping " + rate_per_60sec + " " + shipping.plural + "/min ";
            if (limited_by_production) ss += "(limited by production)";
        }
        return (ss + "\n" + trade_hub?.receiving_info()).Trim();
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] inters;

    public player_interaction[] player_interactions()
    {
        if (inters == null) inters = new player_interaction[] { new cart_menu(this) };
        return inters;
    }

    class cart_menu : left_player_menu
    {
        trade_cart cart;
        UnityEngine.UI.InputField input;
        bool check_on_close = false;

        public cart_menu(trade_cart cart) : base("trade cart") { this.cart = cart; }

        void set_send_id()
        {
            string text = input.text.Replace("e", "");

            if (!int.TryParse(text, out int id))
            {
                popup_message.create("id must be an integer!");
                return;
            }

            if (!cart.trade_hub.set_destination(id, out string err))
            {
                popup_message.create(err);
                return;
            }
        }

        protected override void on_open()
        {
            input.text = cart.trade_hub.current_destination_id.ToString();
            check_on_close = false;
        }

        protected override void on_close()
        {
            if (check_on_close) set_send_id();
        }

        protected override RectTransform create_menu()
        {
            var ui = Resources.Load<RectTransform>("ui/trade_cart").inst();
            input = ui.GetComponentInChildren<UnityEngine.UI.InputField>();
            input.onEndEdit.AddListener((new_val) => { set_send_id(); check_on_close = false; });
            input.onValueChanged.AddListener((s) => { check_on_close = true; });

            foreach (var t in ui.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (t.name == "cart_id")
                {
                    t.text = "" + cart.trade_hub?.network_id;
                    break;
                }

            return ui;
        }
    }
}