using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class random_foliage : world_object.sub_generator
{
    public string directory;

    public float min_scale = 0.7f;
    public float max_scale = 1.4f;
    public float nothing_probability = 0.35f;

    public override void generate(biome.point point, chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        if (chunk.random.range(0, 1f) < nothing_probability) return;

        var options = Resources.LoadAll<GameObject>(directory);

        if (options.Length == 0)
            throw new System.Exception("No options found in the directory: "+directory);

        var generated = options[chunk.random.Next() % options.Length].inst();
        generated.transform.SetParent(transform);
        generated.transform.localPosition = Vector3.zero;
        generated.transform.localRotation = Quaternion.identity;
        generated.transform.localScale = Vector3.one * chunk.random.range(min_scale, max_scale);
        generated.transform.Rotate(Vector3.up, chunk.random.range(0f, 360f), Space.World);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawSphere(Vector3.zero, 0.5f);
    }
}
