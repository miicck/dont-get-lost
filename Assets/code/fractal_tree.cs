using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fractal_tree : MonoBehaviour
{
    class branch
    {
        branch parent;
        List<branch> children;
        int[] tris;

        public branch(Vector3 start, Vector3 end, float angle_between_children,
            int max_child_count, float child_prob, float thickness, float curlyness, int depth,
            ref List<Vector3> verticies, ref List<int> triangles, ref List<Vector3> endpoints, branch parent)
        {
            this.parent = parent;

            // Construct local coordinate system oriented
            // with the branch, but with a fixed chirality
            // (defined by that of the world axes)
            Vector3 up = (end - start).normalized;

            Vector3 right = Vector3.right;
            right -= Vector3.Project(right, up);
            right.Normalize();

            Vector3 forward = Vector3.forward;
            forward -= Vector3.Project(forward, up);
            forward -= Vector3.Project(forward, right);
            forward.Normalize();

            // Get the indicies of the end of the parent
            // branch (or the start of this branch if we have no parent)
            int[] parent_tris = null;
            if (parent == null)
            {
                // No parent => I am the root, create the root verticies
                verticies.Add(start + forward * thickness);
                verticies.Add(start + (right - forward) * thickness / 1.4f);
                verticies.Add(start + (-right - forward) * thickness / 1.4f);

                parent_tris = new int[]
                {
                    verticies.Count - 3,
                    verticies.Count - 2,
                    verticies.Count - 1
                };
            }
            else parent_tris = parent.tris;

            // Reasons not to generate children
            if (thickness < 0.05f ||                      // We've reached minimum thickness
                Mathf.Pow(max_child_count, depth) > 200)  // Upper bound on branch count too high 
            {
                // We are a last branch, taper to a point
                verticies.Add(end);
                endpoints.Add(end);
                int tri = verticies.Count - 1;
                triangles.AddRange(new int[]
                {
                    tri, parent_tris[0], parent_tris[1],
                    tri, parent_tris[1], parent_tris[2],
                    tri, parent_tris[2], parent_tris[0]
                });

                return;
            }

            // Add the vericies at the end of this branch to the vertex array
            verticies.Add(end + forward * thickness);
            verticies.Add(end + (right - forward) * thickness / 1.4f);
            verticies.Add(end + (-right - forward) * thickness / 1.4f);

            tris = new int[]
            {
                verticies.Count - 3,
                verticies.Count - 2,
                verticies.Count - 1,
            };

            // Create the triangles linking me to my parent
            triangles.AddRange(new int[]{

                // First two vericies of triangle belong to me
                tris[1], tris[0], parent_tris[0],
                tris[0], tris[2], parent_tris[2],
                tris[2], tris[1], parent_tris[1],

                // First two verticies of triangle belong to parent
                parent_tris[0], parent_tris[1], tris[1],
                parent_tris[2], parent_tris[0], tris[0],
                parent_tris[1], parent_tris[2], tris[2],
            });

            // Generate my child branches
            children = new List<branch>();

            // Decide which children to generate (uniform distribution
            // but a minimum of 1 child is enforced so we dont end up
            // with incorrect ends)
            int child_count = 0;
            bool[] generate_child = new bool[max_child_count];
            for (int i = 0; i < max_child_count; ++i)
            {
                generate_child[i] = Random.Range(0, 1f) > child_prob;
                if (generate_child[i]) child_count += 1;
            }
            if (child_count == 0)
                generate_child[Random.Range(0, max_child_count)] = true;

            for (int i = 0; i < max_child_count; ++i)
            {
                if (!generate_child[i]) continue;

                Vector3 delta = end - start;
                float child_angle = angle_between_children * (i - max_child_count / 2);
                if (depth % 2 == 0) delta = Quaternion.Euler(child_angle, 0, 0) * delta;
                else delta = Quaternion.Euler(0, 0, child_angle) * delta;
                delta = Quaternion.Euler(0, 45, 0) * delta;
                if (delta.y < 0) delta.y = -delta.y;
                if (delta.y < thickness) delta.y = thickness;

                children.Add(new branch(end, end + delta, angle_between_children,
                    max_child_count, child_prob, thickness * 0.75f, curlyness, depth + 1,
                    ref verticies, ref triangles, ref endpoints, this));
            }
        }

    }

    private void Start()
    {
        if (transform.position.y < world.SEA_LEVEL)
            return; // Don't generate underwater

        List<Vector3> endpoints = new List<Vector3>();
        List<Vector3> verticies = new List<Vector3>();
        List<int> triangles = new List<int>();

        float init_height = Random.Range(2f, 4f);
        float curlyness = 0;
        if (Random.Range(0, 5) == 0)
            curlyness = Random.Range(-90f, 90f);
        curlyness = 0;

        var root = new branch(
            Vector3.zero,               // Start
            Vector3.up * init_height,   // End
            Random.Range(15f, 45f),     // Angle between children
            Random.Range(2, 5),         // Child count
            Random.Range(0.25f, 0.75f), // Child probablility
            0.75f,                      // Thickness
            curlyness,                  // Curlyness
            0,                          // Depth
            ref verticies,              // Verticies
            ref triangles,              // Triangles
            ref endpoints,              // Locations of branch tips
            null                        // Parent branch
        );

        var mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = new Mesh();
        mf.mesh.vertices = verticies.ToArray();
        mf.mesh.triangles = triangles.ToArray();
        mf.mesh.RecalculateNormals();

        var mr = gameObject.AddComponent<MeshRenderer>();
        var mat = Resources.Load<Material>("materials/standard_shader/bark");
        mr.materials = new Material[] { mat };

        var mc = gameObject.AddComponent<MeshCollider>();
    }
}
