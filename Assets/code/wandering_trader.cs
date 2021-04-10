using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wandering_trader : trader, IExtendsNetworked
{
    public override int get_stock(string item) { return stock[item]; }
    public override void set_stock(string item, int count)
    { 
        stock[item] = count;
    }

    public override string display_name() { return "Wandering trader"; }

    public override Dictionary<string, int> get_stock()
    {
        Dictionary<string, int> ret = new Dictionary<string, int>();
        foreach (var kv in stock)
            ret[kv.Key] = kv.Value;
        return ret;
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_string_counts stock;

    public void init_networked_variables()
    {
        stock = new networked_variables.net_string_counts();
        GetComponent<character>().add_register_listener(init_stock);
    }

    void init_stock()
    {
        if (stock.count == 0)
        {
            // Stock needs initializing
            stock["apple"] = 10;
        }
    }
}