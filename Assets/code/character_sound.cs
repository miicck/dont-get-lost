using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class character_sound : MonoBehaviour
{
    public enum TYPE
    {
        IDLE,
        INJURY
    }

    public TYPE type;
    public AudioClip clip;
    public float volume = 1f;
    public float pitch_modifier = 1f;
    public float probability = 1f;
}
