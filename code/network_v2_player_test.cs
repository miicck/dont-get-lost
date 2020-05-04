using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class network_v2_player_test : networked_player
{
    public bool local;

    networked_v2 equipped;

    networked_variable.net_float red_level = new networked_variable.net_float();
    networked_variable.net_float yrot = new networked_variable.net_float();

    private void Start()
    {
        red_level.on_change = (r) =>
        {
            var rend = GetComponentInChildren<Renderer>();
            var col = rend.material.color;
            col.r = r;
            rend.material.color = col;
        };

        yrot.on_change = (y) =>
        {
            transform.rotation = Quaternion.Euler(0, y, 0);
        };

        render_range = 5f;
    }

    private void Update()
    {
        if (local)
        {
            float sw = Input.GetAxis("Mouse ScrollWheel");
            if (sw > 0) render_range *= 1.2f;
            else if (sw < 0) render_range /= 1.2f;

            if (Input.GetKeyDown(KeyCode.R))
            {
                red_level.value += 0.25f;
                if (red_level.value > 1f) red_level.value = 0;
            }

            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) move += transform.forward * Time.deltaTime;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward * Time.deltaTime;

            if (Input.GetKey(KeyCode.LeftShift))
                move *= 10f;
            networked_position += move;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (equipped == null)
                    client.create(transform.position, "network_v2_test/bomb");
            }

            if (Input.GetKey(KeyCode.D)) yrot.value += Time.deltaTime * 100;
            if (Input.GetKey(KeyCode.A)) yrot.value -= Time.deltaTime * 100;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (equipped == null)
                    equipped = client.create(transform.position, "network_v2_test/sword",
                        parent: this, rotation: transform.rotation);
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
