using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class compass : MonoBehaviour
{
    public Camera player_camera;
    public UnityEngine.UI.Image image;
    public UnityEngine.UI.Text coords_text;
    RectTransform image_rect;
    public float angle;

    private void Start()
    {
        image_rect = image.GetComponent<RectTransform>();
    }

    private void Update()
    {
        angle = utils.xz_angle(player_camera.transform.forward) - 90f;
        image_rect.anchoredPosition = new Vector3(angle * 128f / 90f, 0, 0);

        string x = "" + (int)player_camera.transform.position.x;
        string z = "" + (int)player_camera.transform.position.z;
        while (x.Length < 10) x = " " + x;
        while (z.Length < 10) z = z + " ";
        coords_text.text = x + " | " + z;
    }
}
