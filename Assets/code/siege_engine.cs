using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class siege_engine : character_walk_to_interactable, IAddsToInspectionText
{
    public character target { get; protected set; } = null;

    public override string added_inspection_text()
    {
        return base.added_inspection_text() + "\n" +
        (target == null ? "No target" : "Target: " + target.display_name);
    }

    //################################//
    // character_walk_to_interactable //
    //################################//

    public override string task_summary() => "Operating siege weapon: " + GetComponentInParent<building_material>()?.display_name;
}
