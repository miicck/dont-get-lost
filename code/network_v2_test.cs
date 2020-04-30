using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class network_v2_test : MonoBehaviour
{
    public UnityEngine.UI.Button server_start_button;
    public UnityEngine.UI.Button client_start_button;

    void Start()
    {
        server_start_button.onClick.AddListener(() =>
        {
            server.start(
                6969, 10f, "save",
                "network_v2_test/local_player",
                "network_v2_test/remote_player",
                Vector3.zero);
        });

        client_start_button.onClick.AddListener(() =>
        {
            string username = Application.isEditor ? "mick (editor)" : "mick (build)";

            client.connect(
                network_utils.local_ip_address().ToString(),
                6969, username, "password");
        });

    }

    private void Update()
    {
        server.update();
        client.update();
    }
}
