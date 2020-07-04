using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal : building_material
{
    public Transform path_start;

    path path;

    bool path_success(Vector3 end)
    {
        return (end - path_start.position).magnitude > 32f;
    }

    //#################//
    // Unity callbacks //
    //#################//

    private void Start()
    {
        InvokeRepeating("create_pulse", 0.2f, 0.2f);
    }

    private void Update()
    {
        // Updates only happen on the authority client
        if (!has_authority)
            return;

        // Don't work out the path until the chunk is generated
        if (chunk.at(path_start.position, generated_only: true) == null)
            return;

        if (path == null || path.state == path.STATE.FAILED)
            path = new random_path(path_start.position, path_success, path_success, new portal_pather());
        else
        {
            switch (path.state)
            {
                case path.STATE.SEARCHING:
                    path.pathfind(load_balancing.iter);
                    break;

                case path.STATE.COMPLETE:
                    if (Time.frameCount % 2 == 0)
                        path.optimize(load_balancing.iter);
                    else if (!path.validate(load_balancing.iter))
                        path = null;
                    break;
            }
        }
    }

    void create_pulse()
    {
        if (path == null) return;
        if (path.state != path.STATE.COMPLETE) return;

        var pulse = new GameObject("pulse").AddComponent<portal_path_display>();
        pulse.transform.SetParent(transform);
        pulse.transform.position = path[path.length - 1];
        pulse.portal = this;
    }

    void OnDrawGizmosSelected()
    {
        path?.draw_gizmos();
    }

    class portal_pather : IPathingAgent
    {
        public static string last_reason;

        public Vector3 validate_position(Vector3 v, out bool valid)
        {
            return pathfinding_utils.validate_walking_position(v, resolution, out valid);
        }

        public bool validate_move(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            if (Vector3.Angle(delta, Vector3.up) < 40) return false; // Maximum incline of 50 degrees
            bool valid = pathfinding_utils.validate_walking_move(a, b, 1f, 2f, 0.5f, out string reason);
            if (!valid) last_reason = reason;
            return valid;
        }

        public float resolution { get => 0.5f; }
    }

    class portal_path_display : MonoBehaviour
    {
        const float SPEED = 10f;

        public portal portal;

        private void Start()
        {
            var trail = Resources.Load<GameObject>("particle_systems/portal_trail_renderer").inst();
            trail.transform.SetParent(transform);
            trail.transform.localPosition = Vector3.up;
        }

        int progress = 0;
        private void Update()
        {
            if (portal?.path == null || progress >= portal.path.length)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targ = portal.path[portal.path.length - progress - 1];
            Vector3 delta = targ - transform.position;
            if (delta.magnitude < 0.25f)
                progress += 1;

            if (delta.magnitude > Time.deltaTime * SPEED)
                delta = delta.normalized * Time.deltaTime * SPEED;

            transform.position += delta;
        }
    }
}
