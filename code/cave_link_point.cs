using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cave_link_point : MonoBehaviour
{
    public bool link_to_surface = false;

    public cave_link_point linked_to
    {
        get => _linked_to;
        set
        {
            if (value._linked_to != null || _linked_to != null)
                throw new System.Exception("Tried to overwrite link!");

            value._linked_to = this;
            _linked_to = value;
        }
    }
    cave_link_point _linked_to;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (link_to_surface)
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up);

        if (linked_to == null) return;
        Gizmos.DrawLine(transform.position, linked_to.transform.position);
    }
}
