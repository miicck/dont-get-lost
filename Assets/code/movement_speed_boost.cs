using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class movement_speed_boost : MonoBehaviour, IAddsToInspectionText
{
    public float speed_multiplier = 1f;

    public string added_inspection_text()
    {
        int perc = (int)((speed_multiplier - 1f) * 100f);
        return "Movement speed " + (perc < 0 ? "-" : "+") + perc + "%";
    }
}
