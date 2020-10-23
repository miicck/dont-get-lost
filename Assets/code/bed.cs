using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bed : settler_interactable
{
    public const float TIREDNESS_RECOVERY_RATE = 100f / 60f;
    public Transform sleep_orientation;

    public override bool interact(settler s, float time_elapsed)
    {
        s.transform.position = sleep_orientation.position;
        s.transform.rotation = sleep_orientation.rotation;
        s.tiredness -= TIREDNESS_RECOVERY_RATE * Time.deltaTime;
        return s.tiredness < 20f && time_elapsed > 5f;
    }
}
