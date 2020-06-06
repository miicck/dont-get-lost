using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class projectile : item
{
    public override float position_lerp_speed()
    {
        return 20f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.transform.IsChildOf(player.current.transform))
            return;

        Destroy(FindObjectOfType<Rigidbody>());
    }
}
