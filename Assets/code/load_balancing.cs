using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class load_balancing
{
    const float SMOOTHING_AMT = 0.99f;
    const int MIN_ITER = 4;
    const int ITER_PER_EXTRA_FPS = 1;
    const int VSYNC_WINDOW = 1;
    const float MAX_VSYNC_BOOST = 10;

    static float smoothed_dt = 1 / (float)60;
    static float vboost = 0;
    static int smoothed_fps = 60;
    static int target_fps = 60;
    static int extra_fps = 0;
    static int extra_iter = 0;

    public static bool enabled = true;

    /// <summary> Update the timing information needed to perform load balancing. </summary>
    public static void update()
    {
        target_fps = game.loading ? game.LOADING_TARGET_FRAMERATE : Screen.currentResolution.refreshRate;
        smoothed_dt = SMOOTHING_AMT * smoothed_dt + (1 - SMOOTHING_AMT) * Time.deltaTime;
        smoothed_fps = Mathf.FloorToInt(1 / smoothed_dt);
        extra_fps = Mathf.Max(0, smoothed_fps - target_fps);

        if (smoothed_fps >= target_fps - VSYNC_WINDOW &&
            smoothed_fps <= target_fps + VSYNC_WINDOW)
        {
            // Within the vsync window => increase vboost
            vboost += MAX_VSYNC_BOOST * Time.deltaTime;
        }
        vboost *= 1f - Time.deltaTime;

        extra_iter = Mathf.Max(extra_fps * ITER_PER_EXTRA_FPS, (int)vboost);
        extra_iter = (int)Mathf.Pow(extra_iter, 0.5f);
        if (!enabled) extra_iter = 0;
    }

    /// <summary> The number of iterations of compute-heavy tasks to 
    /// carry out. Is always at least <see cref="MIN_ITER"/> so heavy 
    /// tasks always progress, and increases if the frame rate is 
    /// high enough. </summary>
    public static int iter
    {
        get
        {
            if (extra_iter <= 0) return MIN_ITER;
            return Random.Range(MIN_ITER, MIN_ITER + extra_iter + 1);
        }
    }

    public const int MAX_FRAME_INTERVAL = 10;
    public const int MIN_FRAME_INTERVAL = 1;

    /// <summary> Based on the render range from the current player, give a
    /// frame interval (i.e number of frames) that update() calls could
    /// be slowed down to in order to maintain performance as things
    /// interact with the render range boundary (e.g, a town unloading places
    /// unreasonable load on the settler pathfinding, so pathfinding might 
    /// run on this interval rather than every frame).
    /// Will be MIN_FRAME_INTERVAL close to the player and MAX_FRAME_INTERVAL
    /// at the edge of the render distance from the player. </summary>
    public static int render_range_frame_interval(Vector3 position)
    {
        if (player.current == null) return MIN_FRAME_INTERVAL;

        float dist = (position - player.current.transform.position).magnitude;
        dist /= player.current.render_range;

        if (dist > 1) return MAX_FRAME_INTERVAL;
        if (dist < 0) return MIN_FRAME_INTERVAL;

        float fi = MAX_FRAME_INTERVAL * dist + MIN_FRAME_INTERVAL * (1 - dist);
        return Mathf.Clamp((int)fi, MIN_FRAME_INTERVAL, MAX_FRAME_INTERVAL);
    }

    public static bool skip_frame_render_range(Vector3 position) =>
        Time.frameCount % render_range_frame_interval(position) != 0;

    public static string info()
    {
        return "    Smoothed/target FPS : " + smoothed_fps + "/" + target_fps + "\n" +
               "    Extra FPS/V-boost   : " + extra_fps + "/" + vboost.ToString("F2") + "\n" +
               "    Max iter            : " + (MIN_ITER + extra_iter);
    }
}