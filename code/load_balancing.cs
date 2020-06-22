using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class load_balancing
{
    public const int TARGET_FPS = 60;
    public const int ITER_PER_EXTRA_FPS = 1;
    public const float SMOOTHING_AMT = 0.8f;

    static float smoothed_dt = 1 / (float)TARGET_FPS;
    static int smoothed_fps = TARGET_FPS;
    static int extra_fps => Mathf.Max(0, smoothed_fps - TARGET_FPS);
    static int max_iter => 1 + extra_fps * ITER_PER_EXTRA_FPS;

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
            if (max_iter == 1) return 1;
            return Random.Range(1, max_iter);
        }
    }

    public static string info()
    {
        return "    Smoothed fps : " + smoothed_fps + "\n" +
               "    Extra fps    : " + extra_fps + "\n" +
               "    Max iter     : " + max_iter;
    }
}