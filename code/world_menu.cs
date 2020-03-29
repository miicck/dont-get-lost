using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class world_menu : MonoBehaviour
{
    public Button template_button;
    public Text template_header;
    public Transform button_container;
    public Camera menu_camera;

    private void Start()
    {
        // Find all the saved worlds
        var world_folders = System.IO.Directory.GetDirectories(world.worlds_folder());

        var load_header = template_header.inst();
        load_header.text = "Load existing world...";
        if (world_folders.Length == 0) load_header.text = "No existing worlds";
        load_header.transform.SetParent(button_container);

        foreach (var wf in world_folders)
        {
            var button = template_button.inst();
            button.transform.SetParent(button_container);

            string[] splt = wf.Split('/');
            button.GetComponentInChildren<Text>().text = splt[splt.Length - 1];
            button.onClick.AddListener(() => game.load_world(wf));
        }

        if (world_folders.Length > 0)
        {
            var create_header = template_header.inst();
            create_header.text = "Create new world...";
            create_header.transform.SetParent(button_container);
        }

        var new_world = template_button.inst();
        new_world.transform.SetParent(button_container);
        new_world.GetComponentInChildren<Text>().text = "New world";
        new_world.onClick.AddListener(() => game.create_world("world", 6969));

        // Remove the templates
        Destroy(template_button.gameObject);
        Destroy(template_header.gameObject);
    }

    private void Update()
    {
        Color bg = menu_camera.backgroundColor;
        float h, s, v;
        Color.RGBToHSV(bg, out h, out s, out v);
        h += Time.deltaTime / 100f;
        while (h > 1.0f) h -= 1.0f;
        menu_camera.backgroundColor = Color.HSVToRGB(h, s, v);
    }
}
