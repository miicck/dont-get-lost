using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A building that can have item inputs/outputs. </summary>
public class item_logistic_building: building_material
{
    protected List<item_link_point> item_inputs;
    protected List<item_link_point> item_outputs;

    protected virtual void assign_links(
        out List<item_link_point> inputs, 
        out List<item_link_point> outputs)
    {
        inputs = new List<item_link_point>();
        outputs = new List<item_link_point>();

        foreach (var lp in GetComponentsInChildren<item_link_point>())
            switch(lp.type)
            {
                case item_link_point.TYPE.INPUT:
                    inputs.Add(lp);
                    break;

                case item_link_point.TYPE.OUTPUT:
                    outputs.Add(lp);
                    break;

                default:
                    throw new System.Exception("Unkown link point type!");
            }
    }

    protected virtual void Start()
    {
        assign_links(out item_inputs, out item_outputs);
    }
}