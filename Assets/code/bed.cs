using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bed : settler_interactable
{
    public const float TIREDNESS_RECOVERY_RATE = 100f / 60f;
    public Transform sleep_orientation;

    float delta_tired = 0;

    public override bool interact(settler s, float time_elapsed)
    {
        s.transform.position = sleep_orientation.position;
        s.transform.rotation = sleep_orientation.rotation;

        // Beomce un-tired
        delta_tired -= TIREDNESS_RECOVERY_RATE * Time.deltaTime;
        if (delta_tired < -1f)
        {
            delta_tired = 0f;
            s.tiredness.value -= 1;
        }

        // We have to sleep for at least 5 seconds and 
        // until our tiredness drops below 20%
        return s.tiredness.value < 20 && time_elapsed > 5f;
    }
}
