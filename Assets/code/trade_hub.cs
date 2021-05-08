using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class trade_hub : networked
{
    //#####################//
    // Trade hub interface //
    //#####################//

    public item shipping { get; private set; }
    public int rate => rate_sending.value;

    // The object that I represent
    public has_trade_hub owner
    {
        get => try_find_by_id(owner_id.value, false)?.
               GetComponentInChildren<has_trade_hub>();

        set => owner_id.value = value.GetComponentInParent<networked>().network_id;
    }

    // The destination I'm sending items to
    public trade_hub destination { get; private set; }
    public int current_destination_id => destination_id.value;

    // Set the destination that I am sending items to
    public bool set_destination(int destination_id, out string error)
    {
        if (destination_id <= 0)
        {
            error = "Invalid destination id!";
            this.destination_id.value = 0;
            return false;
        }

        if (destination_id == network_id)
        {
            error = "Can't link trade hub to itself!";
            this.destination_id.value = 0;
            return false;
        }

        var th = try_find_by_id(destination_id, false) as trade_hub;
        if (th == null)
        {
            error = "A destination with the given id does not exist!";
            this.destination_id.value = 0;
            return false;
        }

        this.destination_id.value = destination_id;
        error = null;
        return true;
    }

    // Set what I'm sending
    public void set_item_rate(item i, int rate_per_min)
    {
        rate_sending.value = i == null ? 0 : rate_per_min;
        item_sending.value = i?.name;
    }

    // Get information about what I'm receiving
    public string receiving_info()
    {
        Dictionary<item, int> rates = new Dictionary<item, int>();
        foreach (var t in trade_hubs)
            if (t.current_destination_id == network_id)
            {
                var itm = Resources.Load<item>("items/" + t.item_sending.value);
                if (itm == null) continue;
                if (rates.ContainsKey(itm)) rates[itm] += t.rate_sending.value;
                else rates[itm] = t.rate_sending.value;
            }

        if (rates.Count == 0) return "Not receiving anything";

        string ret = "Receiving\n";
        foreach (var kv in rates)
            ret += "  " + kv.Value + " " + kv.Key.plural + "/min\n";

        return ret.Trim();
    }

    //################//
    // Inner workings //
    //################//

    networked_variables.net_int owner_id;
    networked_variables.net_int destination_id;
    networked_variables.net_string item_sending;
    networked_variables.net_int rate_sending;

    public override void on_init_network_variables()
    {
        owner_id = new networked_variables.net_int();
        destination_id = new networked_variables.net_int();
        item_sending = new networked_variables.net_string();
        rate_sending = new networked_variables.net_int();

        item_sending.on_change = () =>
        {
            shipping = Resources.Load<item>("items/" + item_sending.value);
        };

        destination_id.on_change = () =>
        {
            destination = try_find_by_id(destination_id.value, false) as trade_hub;
        };
    }

    // Trade hubs are always loaded
    public override float network_radius() { return Mathf.Infinity; }

    // Register callbacks
    public delegate void on_register_callback();
    public on_register_callback on_register;
    public override void on_first_register() { on_register?.Invoke(); }

    float amt_sent = 0;

    private void Update()
    {
        // Note the below takes place on all clients
        // (even non-auth clients) so that owner.add_item
        // is called on every client.

        // Check we have everything we need to send stuff
        if (shipping == null) return;
        destination_id.on_change();
        if (destination == null) return;

        // Send items at the requested rate
        amt_sent += rate * Time.deltaTime / 60f;
        if (amt_sent > 1)
        {
            amt_sent = 0;
            destination.receive(shipping);
        }
    }

    private void receive(item i)
    {
        owner?.add_item(i);
    }

    private void Start() { trade_hubs.Add(this); }
    private void OnDestroy() { trade_hubs.Remove(this); }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<trade_hub> trade_hubs = new HashSet<trade_hub>();
}