using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class projectile : item
{
    public float start_distance = 1f;
    string got_stuck_in;

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

        // Get stuck in whatever I hit
        got_stuck_in = collision.collider.name;
        Destroy(FindObjectOfType<Rigidbody>());
    }

    /// <summary> Allow easier picking up of projectiles if one is equipped. </summary>
    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        var ray = player.current.camera_ray(player.INTERACTION_RANGE, out float distance);
        foreach (var hit in Physics.RaycastAll(ray, distance))
        {
            var proj = hit.transform.GetComponent<projectile>();
            if (proj != null) proj.pick_up();
        }

        return use_result.complete;
    }

    public override bool allow_left_click_held_down()
    {
        return true;
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
