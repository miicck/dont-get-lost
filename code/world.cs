using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world : MonoBehaviour
{
    public const float MAX_ALTITUDE = 64f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        chunk.generate(0, 0);
        player.create();
    }

    public static float altitude(float x, float z)
    {
        return MAX_ALTITUDE *
            Mathf.PerlinNoise(x / MAX_ALTITUDE, z / MAX_ALTITUDE);
    }
}