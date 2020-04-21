using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_monobehaviour_test : MonoBehaviour
{
    class chunk : top_level_networked_monobehaviour
    {
        int x; int z;

        // Set the x and z coordinates
        protected override void on_create(object[] creation_args)
        {
            x = (int)creation_args[0];
            z = (int)creation_args[1];
            name = "chunk " + x + " " + z;
        }

        // A chunk is compared by it's x and z coordinates
        protected override byte[] comparison_bytes()
        {
            byte[] ret = new byte[sizeof(int) * 2];
            var x_bytes = System.BitConverter.GetBytes(x);
            var z_bytes = System.BitConverter.GetBytes(z);
            System.Buffer.BlockCopy(x_bytes, 0, ret, 0, x_bytes.Length);
            System.Buffer.BlockCopy(z_bytes, 0, ret, x_bytes.Length, z_bytes.Length);
            return ret;
        }
    }

    class item : networked_monobehaviour
    {

    }

    void Start()
    {
        server.start(6969);
        networked_monobehaviour.connect_to_server(server.ip.ToString(), server.port);
    }

    int chunk_count = 0;
    chunk last_chunk;

    void Update()
    {
        server.update();
        networked_monobehaviour.update();

        if (Input.GetKeyDown(KeyCode.C))
            last_chunk = top_level_networked_monobehaviour.create<chunk>(++chunk_count, 0);

        if (Input.GetKeyDown(KeyCode.I))
            networked_monobehaviour.create<item>(last_chunk);
    }
}