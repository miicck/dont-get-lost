using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class network_v2_player_test : networked_player
{
    public bool local;

    networked_v2 equipped;

    private void Start()
    {
        render_range = 5f;
    }

    private void Update()
    {
        if (local)
        {
            float sw = Input.GetAxis("Mouse ScrollWheel");
            if (sw > 0) render_range *= 1.2f;
            else if (sw < 0) render_range /= 1.2f;

            if (Input.GetKey(KeyCode.W)) networked_position += Vector3.forward * Time.deltaTime;
            if (Input.GetKey(KeyCode.S)) networked_position -= Vector3.forward * Time.deltaTime;
            if (Input.GetKey(KeyCode.D)) networked_position += Vector3.right * Time.deltaTime;
            if (Input.GetKey(KeyCode.A)) networked_position -= Vector3.right * Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
                client.create(transform.position, "network_v2_test/bomb");

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (equipped == null)
                    equipped = client.create(transform.position, "network_v2_test/sword", parent: this);
                else
                {
                    equipped.delete();
                    equipped = null;
                }
            }
        }
        else
        {
            if (Input.GetKey(KeyCode.I)) networked_position += Vector3.forward * Time.deltaTime;
            if (Input.GetKey(KeyCode.K)) networked_position -= Vector3.forward * Time.deltaTime;
            if (Input.GetKey(KeyCode.L)) networked_position += Vector3.right * Time.deltaTime;
            if (Input.GetKey(KeyCode.J)) networked_position -= Vector3.right * Time.deltaTime;
        }
    }
}
