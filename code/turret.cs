using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class turret : MonoBehaviour
{
    public float range = 10f;
    public float fire_cooldown = 2f;
    public int attack_damage = 5;

    portal defending
    {
        get
        {
            if (_defending == null)
                _defending = FindObjectOfType<portal>();
            return _defending;
        }
    }
    portal _defending;

    character target
    {
        get
        {
            if (_target == null)
                _target = null; // Target has been deleted
            return _target;
        }

        set
        {
            _target = value;
        }
    }
    character _target;

    private void Start()
    {
        InvokeRepeating("aquire_target", 4f, 4f);
    }

    void aquire_target()
    {
        if (defending == null) return;

        var nearest = utils.find_to_min(defending.GetComponentsInChildren<character>(),
            (c) => (c.transform.position - transform.position).magnitude);

        if (nearest == null) return;


        if ((nearest.transform.position - transform.position).magnitude < range)
            target = nearest;
    }

    void idle()
    {
        Vector3 euler = Quaternion.identity.eulerAngles;
        euler.y = 90f * Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2f / 4f);
        transform.localRotation = Quaternion.Euler(euler);
    }

    float last_fired = 0;

    void attack()
    {
        transform.LookAt(target.transform);
        if (Time.realtimeSinceStartup - last_fired > fire_cooldown)
        {
            last_fired = Time.realtimeSinceStartup;
            target.take_damage(attack_damage);
        }
    }

    void Update()
    {
        if (target == null) idle();
        else attack();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
