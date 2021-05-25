using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class load_balancing
{
    const int ITER_PER_EXTRA_FPS = 1;
    const float SMOOTHING_AMT = 0.6f;

    static float smoothed_dt = 1 / (float)60;
    static int smoothed_fps = 60;

    static int target_fps => game.loading ? game.LOADING_TARGET_FRAMERATE : Screen.currentResolution.refreshRate;
    static int extra_fps => Mathf.Max(0, smoothed_fps - target_fps);
    static int extra_iter => extra_fps * ITER_PER_EXTRA_FPS;
    static int min_iter => 4;

    /// <summary> Update the timing information needed to perform load balancing. </summary>
    public static void update()
    {
        smoothed_dt = SMOOTHING_AMT * smoothed_dt + (1 - SMOOTHING_AMT) * Time.deltaTime;
        smoothed_fps = Mathf.FloorToInt(1 / smoothed_dt);
    }

    /// <summary> The number of iterations of compute-heavy tasks to 
    /// carry out. Is always at least 1 so heavy tasks always progress, 
    /// and increases if the frame rate is high enough. </summary>
    public static int iter
    {
        get
        {
            if (extra_iter <= 0) return min_iter;
            return Random.Range(min_iter, min_iter + extra_iter + 1);
        }
    }

    public static string info()
    {
        return "    Target FPS   : " + target_fps + "\n" +
               "    Smoothed FPS : " + smoothed_fps + "\n" +
               "    Extra FPS    : " + extra_fps + "\n" +
               "    Max iter     : " + (min_iter + extra_iter);
    }
}