using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Object that allows in-editor, or in-game testing of pathfinding. </summary>
public class path_tester : MonoBehaviour
{
    public bool run_every_frame = false;
    public float resolution = 1f;
    public float max_incline = 45f;
    public float target_range = 10f;
    public int iterations_per_frame = 1;
    path path;

    /// <summary> Run <see cref="iterations_per_frame"/> pathfinding iterations. </summary>
    void run_iterations(int count = -1)
    {
        if (count < 0) count = iterations_per_frame;
        path?.pathfind(count > 0 ? count : int.MaxValue);
    }

    /// <summary> Get a new pathfinding target within <see cref="target_range"/>. </summary>
    void new_target()
    {
        Vector3 target = transform.position +
            Random.onUnitSphere * target_range +
            Vector3.up * target_range;

        // Raycast down to find a sensible target
        if (Physics.Raycast(target, Vector3.down, out RaycastHit hit))
            path = new path(
                transform.position, hit.point,
                transform, 2f, resolution,
                max_incline: max_incline
                );
    }

    private void Update()
    {
        if (path == null) return;
        if (path.state == path.STATE.SEARCHING)
            run_iterations();
    }

    private void OnDrawGizmosSelected()
    {
        // Allow pathfinding to run in editor
        if (run_every_frame && !Application.isPlaying)
            run_iterations();

        // Draw path gismos
        path?.draw_gizmos();
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(path_tester))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var pt = (path_tester)target;

            // Make a little button to set a new target
            if (UnityEditor.EditorGUILayout.Toggle("new target", false))
            {
                pt.new_target();
                UnityEditor.SceneView.RepaintAll();
            }

            // Make a button to run a single iteration
            if (UnityEditor.EditorGUILayout.Toggle("run iteration", false))
            {
                pt.run_iterations(1);
                UnityEditor.SceneView.RepaintAll();
            }

            // Get text about the path
            if (pt.path != null)
                UnityEditor.EditorGUILayout.TextArea(pt.path.info());
        }
    }
#endif
}