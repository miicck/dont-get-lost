using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class network_v2_player_test : networked_v2
{
    public bool local;
    private void Update()
    {
        if (!local) return;
        if (Input.GetKey(KeyCode.W)) transform.position += Vector3.forward * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) transform.position -= Vector3.forward * Time.deltaTime;
        if (Input.GetKey(KeyCode.D)) transform.position += Vector3.right * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) transform.position -= Vector3.right * Time.deltaTime;
    }
}
