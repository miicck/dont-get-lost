using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class equipment_mood_effect : MonoBehaviour, IMoodEffect, IAddsToInspectionText
{
    public int mood_effect;
    public armour_piece equipment => GetComponent<armour_piece>();

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text() => "Mood effect " + mood_effect;

    //#############//
    // IMoodEffect //
    //#############//

    public int delta_mood => mood_effect;
    public string display_name => equipment.display_name;
    public string description => "Wearing " + utils.a_or_an(equipment.display_name) + " " + equipment.display_name;
}
