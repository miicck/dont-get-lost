using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_material : MonoBehaviour
{
    public Sprite sprite;

    public static research_material[] all
    {
        get
        {
            if (_all == null)
                _all = Resources.LoadAll<research_material>("research_materials");
            return _all;
        }
    }
    static research_material[] _all;
}
