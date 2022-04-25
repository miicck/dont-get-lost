using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class siege_engine_projectile : MonoBehaviour
{
    public int damage = 10;

    public character target { get; set; }

    private void Update()
    {
        if (transform.parent != null)
            return; // Still attached to siege engine

        if (target == null || target.is_dead)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 delta = target.projectile_target() - transform.position;
        if (delta.magnitude < 0.25f)
        {
            if (target.has_authority)
                target.take_damage(damage);

            Destroy(gameObject);
            return;
        }

        transform.position += delta.normalized * Time.deltaTime * 15f;
    }
}