using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bed : settler_interactable
{
    public const float TIREDNESS_RECOVERY_RATE = 100f / 60f;
    public Transform sleep_orientation;

    float delta_tired;
    float time_slept;

    public override void on_assign(settler s)
    {
        // Reset stuff
        time_slept = 0f;
        delta_tired = 0f;
    }

    public override void on_interact(settler s)
    {
        // Lie down
        s.transform.position = sleep_orientation.position;
        s.transform.rotation = sleep_orientation.rotation;

        time_slept += Time.deltaTime;

        // Only modify tiredness on authority client
        if (!s.has_authority) return;

        // Beomce un-tired
        delta_tired -= TIREDNESS_RECOVERY_RATE * Time.deltaTime;
        if (delta_tired < -1f)
        {
            delta_tired = 0f;
            s.tiredness.value -= 1;
        }
    }

    public override bool is_complete(settler s)
    {
        // We have to sleep for at least 5 seconds and 
        // until our tiredness drops below 20%
        return s.tiredness.value < 20 && time_slept > 5f;
    }

    public override void on_unassign(settler s)
    {
        // Un-lie down
        s.transform.rotation = Quaternion.identity;
    }
}
