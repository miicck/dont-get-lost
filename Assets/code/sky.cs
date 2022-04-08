using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sky : MonoBehaviour
{
    public Renderer sky_background;

    void Update()
    {
        if (player.current == null)
            return;
        transform.position = player.current.transform.position;
    }

    public Color color
    {
        get => utils.get_color(sky_background.material);
        set
        {
            if (player.current != null)
            {
                var hd_cam = player.current.camera.GetComponent<
                    UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
                if (hd_cam != null)
                    hd_cam.backgroundColorHDR = value;

                player.current.camera.clearFlags = CameraClearFlags.Color;
                player.current.camera.backgroundColor = value;
            }

            utils.set_color(sky_background.material, value);
        }
    }

    public static sky instance
    {
        get
        {
            if (_sky == null)
                _sky = Resources.Load<sky>("misc/sky").inst();
            return _sky;
        }
    }
    static sky _sky;
}
