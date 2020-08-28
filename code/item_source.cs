using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_source : MonoBehaviour
{
    public item item;
    public float time_between_items = 1f;
    item_link_point[] link_points;

    float last_create_time = 0;

    private void Start()
    {
        link_points = GetComponentsInChildren<item_link_point>();
        if (link_points.Length == 0)
            throw new System.Exception("Item source has no link points!");
    }

    private void Update()
    {
        if (item == null) return;
        if (Time.time > last_create_time + time_between_items)
        {
            last_create_time = Time.time;
            create();
        }
    }

    int items_created = 0;
    void create()
    {
        var output_link = link_points[items_created % link_points.Length];
        if (output_link.type != item_link_point.TYPE.OUTPUT)
            throw new System.Exception("Item source link is not marked as output!");

        if (output_link.linked_to == null)
            return; // Don't spawn anything unless we're linked up

        if (output_link.item != null)
            return; // Output backed up, don't spawn anything

        // Spawn the new item
        output_link.item = item.create(item.name, 
            output_link.position, output_link.transform.rotation);
        ++items_created;
    }
}
