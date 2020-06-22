using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pathfinding_agent : networked, IPathingAgent
{
    public float agent_width = 1f;
    public float agent_height = 1.5f;
    public float agent_ground_clearance = 0.5f; 
    public float base_speed = 5f;

    protected delegate void callback();

    path path;
    int path_progress;
    Vector3 target;
    callback on_fail;
    callback on_arrive;
    callback on_move;
    callback on_search;

    // Overridable methods
    protected virtual float speed() { return base_speed; }
    protected virtual float arrive_distance() { return 0.25f; }
    protected virtual int pathfind_iters_per_frame() { return 1; }
    protected virtual bool path_constriant(Vector3 v) { return true; }

    /// <summary> Move agent towards <paramref name="v"/>, returns true if
    /// we're considered to have arrived at <paramref name="v"/>. </summary>
    bool move_towards(Vector3 v)
    {
        // Vector to target
        Vector3 delta = v - transform.position;
        bool arrived = delta.magnitude <= arrive_distance();

        // Work out how far to move (cap out movement
        // distance at speed * dt)
        float max_dx = speed() * Time.deltaTime;
        if (delta.magnitude > max_dx)
            delta = max_dx * delta.normalized;

        // Ensure we're facing the direction of travel
        Vector3 fw = new Vector3(delta.x, 0, delta.z);
        if (fw.magnitude > 0.01f)
        {
            fw.Normalize();
            fw = Vector3.Lerp(transform.forward, fw, (5f + speed()) * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(fw, Vector3.up);
        }

        transform.position += delta;
        return arrived;
    }

    /// <summary> Move along the current path, returns true if 
    /// we've completed movement along the path. </summary>
    bool move_along_path()
    {
        if (path_progress >= path.length)
            return true;

        if (move_towards(path[path_progress]))
            ++path_progress;

        return false;
    }

    protected Vector3 random_target(float range)
    {
        Vector3 rt = transform.position + Random.insideUnitSphere * range;
        if (Physics.Raycast(rt + Vector3.up, Vector3.down, out RaycastHit hit, 2f))
            rt = hit.point;
        return rt;
    }

    /// <summary>
    /// Called to set the target.
    /// </summary>
    /// <param name="target">Target to go to.</param>
    /// <param name="on_fail">Called when pathfinding fails.</param>
    /// <param name="on_arrive">Called when we've arrived at the target.</param>
    /// <param name="on_move">Called every frame we're moving towards the target.</param>
    /// <param name="on_search">Called every frame we're searching for a path.</param>
    protected void go_to(Vector3 target, callback on_fail, callback on_arrive,
        callback on_move = null, callback on_search = null)
    {
        if (!has_authority)
            return; // Only clients with authority control agent position

        this.target = target;
        this.on_fail = on_fail;
        this.on_arrive = on_arrive;
        this.on_move = on_move;
        this.on_search = on_search;

        path = new astar_path(transform.position, target, this);
        path_progress = 0;
    }

    /// <summary> Run the pathfinding agent. </summary>
    protected virtual void Update()
    {
        // Nothing to do
        if (path == null)
            return;

        switch (path.state)
        {
            // Pathfinding failed
            case path.STATE.FAILED:
                path = null;
                on_fail?.Invoke();
                break;

            // Pathfinding complete, move along the path
            case path.STATE.COMPLETE:
                on_move?.Invoke();
                if (move_along_path())
                {
                    path = null;
                    on_arrive?.Invoke();
                }
                break;

            // Still searching for a path
            case path.STATE.SEARCHING:
                on_search?.Invoke();
                path.pathfind(pathfind_iters_per_frame());
                break;

            default:
                throw new System.Exception("Unkown path state!");
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (path != null) path.draw_gizmos();
    }

    //###############//
    // IPathingAgent //
    //###############//

    public Vector3 validate_position(Vector3 v, out bool valid)
    {
        Vector3 pos = pathfinding_utils.validate_walking_position(v, resolution, out valid, transform);
        if (!path_constriant(pos)) valid = false;
        return pos;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            agent_width, agent_height, agent_ground_clearance, transform);
    }

    public float resolution { get => Mathf.Min(agent_width, agent_height); }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_float y_rotation;

    public override void on_init_network_variables()
    {
        y_rotation = new networked_variables.net_float(resolution: 10f);

        y_rotation.on_change = () =>
        {
            var ea = transform.rotation.eulerAngles;
            ea.y = y_rotation.value;
            transform.rotation = Quaternion.Euler(ea);
        };
    }

    public override void on_network_update()
    {
        if (!has_authority) return;

        networked_position = transform.position;
        y_rotation.value = transform.rotation.eulerAngles.y;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(pathfinding_agent), true)]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var agent = (pathfinding_agent)target;
            UnityEditor.EditorGUILayout.TextArea(agent.path?.info_text());
        }
    }
#endif
}
