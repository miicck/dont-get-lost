using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weather_effect_particle_system : MonoBehaviour
{
    public weather_effect effect;
    public ParticleSystem particle_system;

    float max_rate_over_time_mult = 0;

    private void Start()
    {
        max_rate_over_time_mult = particle_system.emission.rateOverTimeMultiplier;
    }

    void Update()
    {
        var emission = particle_system.emission;
        emission.rateOverTimeMultiplier = effect.weight * max_rate_over_time_mult;
    }
}
