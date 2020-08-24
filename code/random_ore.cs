using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class random_ore : world_object.sub_generator
{
    public enum TYPE
    {
        SEAM,
    }

    public TYPE type;

    public override void generate(biome.point point,
        chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        var options = Resources.LoadAll<GameObject>(directory());
        var chosen = options[chunk.random.Next() % options.Length].inst();
        chosen.transform.SetParent(transform);
        chosen.transform.localPosition = Vector3.zero;
        chosen.transform.localRotation = Quaternion.identity;
        chosen.transform.localScale = Vector3.one;

        // Apply type-specific randomization
        switch(type)
        {
            case TYPE.SEAM:
                // Ensure x/y scales are the same, so random rotation doesn't
                // take us outside the square
                float minxy = Mathf.Min(transform.localScale.x, transform.localScale.y);
                transform.localScale = new Vector3(minxy, minxy, transform.localScale.z);
                transform.Rotate(0, 0, chunk.random.range(0f, 360f));
                break;

            default:
                throw new System.Exception("Unknown ore type: " + type);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(type_offset(), type_scale());
    }

    Vector3 type_offset()
    {
        switch (type)
        {
            case TYPE.SEAM: return new Vector3(0, 0, -0.05f);
            default: throw new System.Exception("Unknown ore type: " + type);
        }
    }

    Vector3 type_scale()
    {
        switch (type)
        {
            case TYPE.SEAM: return new Vector3(1f, 1f, 0.1f);
            default: throw new System.Exception("Unknown ore type: " + type);
        }
    }

    string directory()
    {
        switch (type)
        {
            case TYPE.SEAM: return "ores/seams/";
            default: throw new System.Exception("Unknown ore type: " + type);
        }
    }
}
