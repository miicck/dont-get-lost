using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fishing_float : networked
{
    public void set_velocity(Vector3 v) => velocity.value = v;

    networked_variables.net_vector3 velocity;

    public override float network_radius() => 100f;

    public override void on_init_network_variables()
    {
        velocity = new networked_variables.net_vector3();
    }

    private void Update()
    {
        if (!has_authority) return; // Don't do anything on non-auth client

        float height = 1f;
        float submerged_amt = Mathf.Clamp(1f - (transform.position.y + height * 0.5f - world.SEA_LEVEL) / height, 0f, 1f);

        // Gravity/bouyancy
        Vector3 new_velocity = velocity.value;
        new_velocity.y -= Time.deltaTime * (10 - submerged_amt * 20);

        // Damping
        new_velocity -= Time.deltaTime * new_velocity * submerged_amt * 10;

        // Apply velocity
        transform.position += new_velocity * Time.deltaTime;

        velocity.value = new_velocity;

        if (submerged_amt > 0.1f)
        {
            if (((int)Time.time) % 10 == Random.Range(0, 10))
                velocity.value -= Vector3.up;
        }
    }
}