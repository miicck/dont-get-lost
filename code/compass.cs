using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class compass : MonoBehaviour
{
    public Camera player_camera;
    public UnityEngine.UI.Image image;
    RectTransform image_rect;
    public float angle;

    private void Start()
    {
        image_rect = image.GetComponent<RectTransform>();
    }

    private void Update()
    {
        angle = utils.compass_angle(player_camera.transform.forward);
        image_rect.anchoredPosition = new Vector3(angle * 128f / 90f, 0, 0);
    }
}
