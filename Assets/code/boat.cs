using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class boat : MonoBehaviour
{
    public Vector3 loading_centre = Vector3.zero;
    public Vector3 loading_size = Vector3.one;

    List<item> items = new List<item>();

    int x_size => 1 + (int)(loading_size.x / item.LOGISTICS_SIZE);
    int y_size => 1 + (int)(loading_size.y / item.LOGISTICS_SIZE);
    int z_size => 1 + (int)(loading_size.z / item.LOGISTICS_SIZE);

    public void add_item(item i)
    {
        // Check if we've reached capacity
        if (items.Count >= x_size * y_size * z_size)
        {
            Destroy(i.gameObject);
            return;
        }

        // Position + add item
        i.transform.position = item_location(items.Count);
        i.transform.SetParent(transform);
        i.transform.localRotation = Quaternion.identity;
        items.Add(i);
    }

    Vector3 item_location(int n)
    {
        // Work out y coordinate
        int y = n / (x_size * z_size);

        // Work out z coordinate
        int z = (n - y * x_size * z_size) / x_size;

        // Work out x coordinate
        int x = n - y * x_size * z_size - z * x_size;

        return transform.TransformPoint(
            x * item.LOGISTICS_SIZE - loading_size.x / 2 + loading_centre.x,
            y * item.LOGISTICS_SIZE - loading_size.y / 2 + loading_centre.y,
            z * item.LOGISTICS_SIZE - loading_size.z / 2 + loading_centre.z
        );
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        for (int i = 0; i < x_size * y_size * z_size; ++i)
            Gizmos.DrawWireSphere(item_location(i), 0.02f);
    }
}
