using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class global_controls : MonoBehaviour
{
    void Start()
    {
        // Persistant through scene loads
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // Toggle fullscreen modes on F11
        if (Input.GetKeyDown(KeyCode.F11))
        {
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.Windowed:
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                case FullScreenMode.FullScreenWindow:
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case FullScreenMode.ExclusiveFullScreen:
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                default:
                    throw new System.Exception("Unkown fullscreen mode!");
            }
        }
    }
}
