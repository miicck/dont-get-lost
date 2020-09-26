using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class respawn_timer : MonoBehaviour
{
    public player to_respawn;
    public UnityEngine.UI.Text countdown_text;

    void update_text()
    {
        countdown_text.text = "Respawn in " + time_left + "...";
    }

    int time_left = 5;
    void Start()
    {
        InvokeRepeating("countdown", 1, 1);
        update_text();
    }

    void countdown()
    {
        --time_left;
        update_text();

        if (time_left <= 0)
        {
            to_respawn.respawn();
            Destroy(gameObject);
        }
    }
}
