using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class turret : MonoBehaviour
{
    public float range = 10f;
    public float fire_cooldown = 2f;
    public int attack_damage = 5;
    public Transform projectile_start;

    float phase;

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
        phase = Random.Range(0, 1f);
        InvokeRepeating("aquire_target", fire_cooldown * phase, fire_cooldown);
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
        euler.y = 90f * Mathf.Sin((phase + Time.realtimeSinceStartup / 4f) * Mathf.PI * 2f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(euler), Time.deltaTime * 10f);
    }

    float last_fired = 0;

    void attack()
    {
        Vector3 target_forward = target.transform.position - transform.position;
        transform.forward = Vector3.Lerp(transform.forward, target_forward, Time.deltaTime * 10f);

        if (Time.realtimeSinceStartup - last_fired > fire_cooldown &&
            Vector3.Angle(transform.forward, target_forward) < 10f)
        {
            last_fired = Time.realtimeSinceStartup;
            var fired = Resources.Load<GameObject>("misc/projectile_trail").inst().AddComponent<projectile>();
            fired.start = projectile_start.position;
            fired.target = target;
            fired.damage = attack_damage;
            fired.speed = 50f;
        }
    }

    class projectile : MonoBehaviour
    {
        public Vector3 start;
        public character target;
        public int damage;
        public float speed;

        private void Start()
        {
            transform.position = start;
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 delta = target.projectile_target.position - transform.position;
            if (delta.magnitude < 0.25f)
            {
                target.take_damage(damage);
                target = null;
            }

            delta = delta.clamp_magnitude(0, Time.deltaTime * speed);
            transform.position += delta;
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
