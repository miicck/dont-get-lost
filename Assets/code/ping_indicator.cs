using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ping_indicator : MonoBehaviour
{
    public Vector3 pinged_position;
    RectTransform rect_transform;

    void Start()
    {
        rect_transform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (player.current == null) return;
        rect_transform.position = utils.clamped_screen_point(player.current.camera, pinged_position, out bool on_edge);
    }
}
