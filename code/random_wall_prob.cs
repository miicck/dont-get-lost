using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class random_wall_prob : world_object.sub_generator
{
    public float probability = 0.5f;
    public Material material;

    public override void generate(biome.point point, chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        if (chunk.random.range(0, 1f) < probability)
            create_wall();
    }

    void create_wall()
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(transform);
        c.transform.localScale = Vector3.one;
        c.transform.localPosition = Vector3.zero;
        c.transform.localRotation = Quaternion.identity;
        c.GetComponentInChildren<MeshRenderer>().material = material;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
    }
}
