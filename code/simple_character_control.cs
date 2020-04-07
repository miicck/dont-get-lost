using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simple_character_control : MonoBehaviour
{
    float speed = 2f;
    float angular_speed = 100f;

    CharacterController controller;
    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    float yvel = 0;

    void Update()
    {
        float local_speed = speed;
        if (Input.GetKey(KeyCode.LeftShift)) local_speed *= 5f;
        if (Input.GetKey(KeyCode.LeftControl)) local_speed /= 5f;

        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) move += transform.forward * local_speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward * local_speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D)) move += transform.right * local_speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) move -= transform.right * local_speed * Time.deltaTime;

        if (!controller.isGrounded) yvel -= 10 * Time.deltaTime; // Gravity
        else if (Input.GetKeyDown(KeyCode.Space)) yvel = 10f; // Jump
        move += Vector3.up * yvel * Time.deltaTime;

        controller.Move(move);

        if (Input.GetKey(KeyCode.Q)) transform.Rotate(0, -Time.deltaTime * angular_speed, 0);
        if (Input.GetKey(KeyCode.E)) transform.Rotate(0, Time.deltaTime * angular_speed, 0);
    }
}
