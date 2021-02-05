using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ingredients_to_coins_product : product
{
    private void Start()
    {
        set_value();
    }

    void set_value()
    {
        item = Resources.Load<item>("items/coin");
        mode = MODE.SIMPLE;

        min_count = 0;
        foreach (var i in GetComponents<ingredient>())
            min_count += Mathf.CeilToInt(i.average_value());
        max_count = min_count;
    }

    private void OnValidate()
    {
        set_value();
    }
}
