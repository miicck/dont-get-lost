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
        Vector3 sp = player.current.camera.WorldToScreenPoint(pinged_position);
        sp.x = Mathf.Clamp(sp.x, 0, Screen.width);
        sp.y = Mathf.Clamp(sp.y, 0, Screen.height);
        rect_transform.position = sp;
    }
}
