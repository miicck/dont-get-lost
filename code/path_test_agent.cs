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
            {
                random_path.success_func f = (v) => (v - transform.position).magnitude > 10f;
                _path = new random_path(transform.position, f, f, this);
                //_path = new astar_path(transform.position,
                //    target.position, this, max_iterations: max_iter);
            }
            return _path;
        }
        set
        {
            _path = value;
            path_progress = 0;
        }
    }
    path _path;
    int path_progress = 0;

    private void Start()
    {
        if (transform.childCount == 0) // Allows duplicating without cube buildup
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.localScale = 0.99f * new Vector3(agent_width, agent_height, agent_width);
            body.transform.SetParent(transform);
            body.transform.localPosition = Vector3.up * agent_height / 2f;
        }

        if (run_once_and_time)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            path.pathfind(int.MaxValue);
            Debug.Log("Pathing finished in " + sw.ElapsedMilliseconds + " ms");
        }
    }

    private void Update()
    {
        switch (path.state)
        {
            case path.STATE.SEARCHING:
                if (iter_per_frame > 0)
                    path.pathfind(iter_per_frame);

                if (Input.GetKeyDown(KeyCode.Space))
                    path.pathfind(1);
                break;

            case path.STATE.COMPLETE:
                if (path_progress < path.length)
                {
                    Vector3 delta = path[path_progress] - transform.position;
                    if (delta.magnitude < resolution / 2f)
                        path_progress += 1;

                    transform.position += delta.normalized * Time.deltaTime * 10;
                }
                else path = null;
                break;

            case path.STATE.FAILED:
                path = null;
                break;

            default:
                throw new System.Exception("Unkown path state!");
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
        return pathfinding_utils.validate_walking_position(v, resolution, out valid);
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b, agent_width, agent_height, ground_clearance);
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
