using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class menu_videos : MonoBehaviour
{
    UnityEngine.Video.VideoPlayer player;
    UnityEngine.Video.VideoClip[] clips;

    const float TRANSITION_TIME = 1f;
    const float PAUSE_TIME = 4f;

    float time => (float)player.time;
    float clip_length => (float)player.clip.length;
    int clip_index = 0;

    float alpha
    {
        get
        {
            if (time < TRANSITION_TIME) return time / TRANSITION_TIME;
            if (time > clip_length - TRANSITION_TIME) return (clip_length - time) / TRANSITION_TIME;
            return 1f;
        }
    }

    void Start()
    {
        player = GetComponent<UnityEngine.Video.VideoPlayer>();
        clips = Resources.LoadAll<UnityEngine.Video.VideoClip>("videos/menu_videos");
        player.clip = clips[0];

        // Start paused, and play after PAUSE_TIME
        player.Pause();
        Invoke("start_player", PAUSE_TIME);

        player.loopPointReached += (e) =>
        {
            // Increment the clip number
            clip_index = (clip_index + 1) % clips.Length;
            player.clip = clips[clip_index];

            // Pause for PAUSE_TIME
            player.Pause();
            Invoke("start_player", PAUSE_TIME);
        };
    }

    void start_player()
    {
        player.Play();
    }

    void Update()
    {
        player.targetCameraAlpha = alpha;
    }
}
