using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weather_effect_particle_system : MonoBehaviour
{
    public weather_effect effect;
    public ParticleSystem particle_system;
    public AudioSource audio_source;

    float max_rate_over_time_mult;
    float max_volume;

    private void Start()
    {
        max_rate_over_time_mult = particle_system.emission.rateOverTimeMultiplier;
        max_volume = audio_source == null ? 0 : audio_source.volume;
    }

    void Update()
    {
        var emission = particle_system.emission;
        emission.rateOverTimeMultiplier = effect.weight * max_rate_over_time_mult;

        if (audio_source != null)
            audio_source.volume = max_volume * effect.weight;
    }
}
