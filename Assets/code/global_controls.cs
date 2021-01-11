using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class global_controls : MonoBehaviour
{
    static bool already_exists = false;

    [RuntimeInitializeOnLoadMethod]
    static void startup()
    {
        version_control.on_startup();
        steam.start();
    }

    private void Start()
    {
        // Ensure only one global_controls is created
        if (already_exists)
        {
            Destroy(gameObject);
            return;
        }

        already_exists = true;

        // Persistant through scene loads
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        steam.update();

        // Cycle fullscreen modes
        if (controls.triggered(controls.BIND.CYCLE_FULLSCREEN_MODES))
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

    private void OnApplicationQuit()
    {
        steam.stop();
    }
}
