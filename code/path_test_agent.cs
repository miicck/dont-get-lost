using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class path_test_agent : MonoBehaviour, IPathingAgent
{
    public Transform target;
    public int max_iter = 1000;
    public int iter_per_frame = 1;
    public int max_depth = 2;
    public bool run_once_and_time = false;
    public float path_resolution = 1f;
    public float agent_height = 2f;
    public float agent_width = 1f;
    public float ground_clearance = 0.25f;

    path path
    {
        get
        {
            if (_path == null)
                _path = new astar_path(transform.position,    
                    target.position, this, max_iterations: max_iter);
            return _path;
        }
    }
    path _path;

    private void Start()
    {
        if (run_once_and_time)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            path.pathfind(int.MaxValue);
            Debug.Log("Pathing finished in " + sw.ElapsedMilliseconds + " ms");
        }
    }

    int i_progresss = 0;
    private void Update()
    {
        if (iter_per_frame > 0)
            path.pathfind(iter_per_frame);

        if (Input.GetKeyDown(KeyCode.Space))
            path.pathfind(1);

        if (path.state == path.STATE.COMPLETE)
        {
            if (i_progresss < path.length)
            {
                Vector3 delta = path[i_progresss] - transform.position;
                if (delta.magnitude < resolution / 2f)
                    i_progresss += 1;

                transform.position += delta.normalized * Time.deltaTime * 10;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Vector3 size = new Vector3(agent_width, agent_height, agent_width);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.up * agent_height / 2, size);
        Gizmos.matrix = Matrix4x4.identity;

        _path?.draw_gizmos();
    }

    //###############//
    // IPathingAgent //
    //###############//

    public Vector3 validate_position(Vector3 v, out bool valid)
    {
        return pathfinding_utils.validate_walking_position(v, resolution, out valid, transform);
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b, agent_width, agent_height, ground_clearance, transform);
    }

    public float resolution { get => path_resolution; }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(path_test_agent))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var a = (path_test_agent)target;
            UnityEditor.EditorGUILayout.TextArea(a._path?.info_text());
        }
    }
#endif
}
