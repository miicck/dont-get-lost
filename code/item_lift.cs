using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift : MonoBehaviour, INonBlueprintable
{
    public item_link_point input
    {
        get
        {
            if (_input == null) assign_points();
            return _input;
        }
    }
    item_link_point _input;

    public item_link_point output
    {
        get
        {
            if (_output == null) assign_points();
            return _output;
        }
    }
    item_link_point _output;

    private void assign_points()
    {
        // Identify/Assign the input/output links
        var pts = GetComponentsInChildren<item_link_point>();
        if (pts.Length != 2)
            throw new System.Exception("An item lift must have exactly 2 links!");

        if (pts[0].type == item_link_point.TYPE.INPUT)
        {
            if (pts[1].type != item_link_point.TYPE.OUTPUT)
                throw new System.Exception("Item lift must have one output and one input!");

            _input = pts[0];
            _output = pts[1];

        }
        else if (pts[0].type == item_link_point.TYPE.OUTPUT)
        {
            if (pts[1].type != item_link_point.TYPE.INPUT)
                throw new System.Exception("Item lift must have one output and one input!");

            _input = pts[1];
            _output = pts[0];
        }
        else throw new System.Exception("Unkown link type: " + pts[0].type + "!");
    }
}
