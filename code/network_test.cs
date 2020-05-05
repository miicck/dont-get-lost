using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class network_v2_test : MonoBehaviour
{
    public UnityEngine.UI.Button server_start_button;
    public UnityEngine.UI.Button client_start_button;
    public UnityEngine.UI.Text debug_info;
    public UnityEngine.UI.InputField username;

    void Start()
    {
        server_start_button.onClick.AddListener(() =>
        {
            server.start(
                6969, "save",
                "network_v2_test/local_player",
                "network_v2_test/remote_player");
        });

        client_start_button.onClick.AddListener(() =>
        {
            client.connect(
                network_utils.local_ip_address().ToString(),
                6969, username.text, "password");
        });

        username.text = Application.isEditor ? "editor" : "build";
    }

    private void Update()
    {
        server.update();
        client.update();

        debug_info.text = server.info() + "\n\n" + client.info();
    }

    private void OnApplicationQuit()
    {
        client.disconnect();
        server.stop();
    }

    private void OnDrawGizmos()
    {
        server.draw_gizmos();
    }
}