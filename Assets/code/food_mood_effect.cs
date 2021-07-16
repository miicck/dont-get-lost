using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class food_mood_effect : MonoBehaviour, IAddsToInspectionText
{
    public mood_effect effect;

    public string added_inspection_text()
    {
        return "Mood effect " + effect.delta_mood;
    }
}
