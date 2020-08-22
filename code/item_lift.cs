using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift : MonoBehaviour
{
    public item_link_point input { get; private set; }
    public item_link_point output { get; private set; }

    private void Start()
    {
        // Identify/Assign the input/output links
        var pts = GetComponentsInChildren<item_link_point>();
        if (pts.Length != 2)
            throw new System.Exception("An item lift must have exactly 2 links!");

        if (pts[0].type == item_link_point.TYPE.INPUT)
        {
            if (pts[1].type != item_link_point.TYPE.OUTPUT)
                throw new System.Exception("Item lift must have one output and one input!");

            input = pts[0];
            output = pts[1];

        }
        else if (pts[0].type == item_link_point.TYPE.OUTPUT)
        {
            if (pts[1].type != item_link_point.TYPE.INPUT)
                throw new System.Exception("Item lift must have one output and one input!");

            input = pts[1];
            output = pts[0];
        }
        else throw new System.Exception("Unkown link type: " + pts[0].type + "!");
    }
}
