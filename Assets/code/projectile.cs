using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class projectile : item, INotPathBlocking
{
    public int damage = 4;
    public float start_distance = 1f;
    string got_stuck_in;

    public override bool persistant()
    {
        // Projectiles despawn
        return false;
    }

    public override float position_lerp_speed()
    {
        // Lerp faster so it looks more like a projectile
        return 20f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Don't get stuck in player, or other projectiles
        if (collision.collider.transform.IsChildOf(player.current.transform)) return;
        if (collision.collider.GetComponent<projectile>() != null) return;

        // Apply damage to characters
        collision.collider.GetComponentInParent<IAcceptsDamage>()?.take_damage(damage);

        // Get stuck in whatever I hit
        got_stuck_in = collision.collider.name;
        Destroy(FindObjectOfType<Rigidbody>());
    }

    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] { new pickup_ammo() };
    }

    class pickup_ammo : player_interaction
    {
        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool allow_held => true;

        public override string context_tip()
        {
            return "pickup projectiles";
        }

        public override bool start_interaction(player player)
        {
            // Don't do anything unless player has authority
            if (!player.has_authority) return true;

            var ray = player.camera_ray(player.INTERACTION_RANGE, out float distance);
            foreach (var hit in Physics.RaycastAll(ray, distance))
            {
                var proj = hit.transform.GetComponent<projectile>();
                if (proj != null) proj.pick_up();
            }

            return true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * start_distance);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(projectile))]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var p = (projectile)target;
            UnityEditor.EditorGUILayout.TextArea("Stuck in: " + p.got_stuck_in);
        }
    }
#endif

}
