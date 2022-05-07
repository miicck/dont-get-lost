using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class uncovered_mood_effect : MonoBehaviour, IAddsToInspectionText
{
    public mood_effect effect;

    public string added_inspection_text()
    {
        if (!weather.spot_is_covered(transform.position))
            return "Exposed to the elements";
        return null;
    }
}
