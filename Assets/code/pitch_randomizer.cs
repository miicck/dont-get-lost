using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pitch_randomizer : MonoBehaviour
{
    public AudioSource source;
    public float min_pitch = 0.95f;
    public float max_pitch = 1.05f;

    void Start()
    {
        source.pitch *= Random.Range(min_pitch, max_pitch);
    }
}
