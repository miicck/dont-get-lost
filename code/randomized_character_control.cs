using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class randomized_character_control : MonoBehaviour
{
    public float time_between_randomizations = 5f;
    public float max_vel_component = 4f;
    public float max_angular_vel = 300f;

    CharacterController controller;
    void Start()
    {
        controller = GetComponent<CharacterController>();
        InvokeRepeating("randomize", 
            Random.Range(0, time_between_randomizations), 
            time_between_randomizations);
    }

    Vector3 velocity;
    float angular_velocity;

    void randomize()
    {
        velocity = new Vector3(Random.Range(-max_vel_component, max_vel_component),
                               0, Random.Range(-max_vel_component, max_vel_component));
        angular_velocity = Random.Range(-max_angular_vel, max_angular_vel);
    }

    void Update()
    {
        transform.Rotate(0, angular_velocity * Time.deltaTime, 0);
        controller.Move(velocity * Time.deltaTime);
        if (!controller.isGrounded) velocity -= 10f * Time.deltaTime * Vector3.up;
    }
}
