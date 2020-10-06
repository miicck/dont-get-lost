using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift : item_logistic_building
{
    public item_link_point input => item_inputs[0];
    public item_link_point output => item_outputs[0];
}
