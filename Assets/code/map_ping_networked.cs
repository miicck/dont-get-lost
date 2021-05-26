using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class map_ping_networked : networked
{
    public const float PING_TIMEOUT = 5f;

    ping_indicator ui;
    networked_variables.net_color color;

    public override void on_init_network_variables()
    {
        // Create the ui ping thing (created here so it's guaranteed to
        // exist for the color.on_change function). It will be in the
        // wrong place until on_create() is called, so disable it for now.
        ui = Resources.Load<ping_indicator>("ui/ping_indicator").inst();
        ui.transform.SetParent(game.canvas.transform);
        ui.gameObject.SetActive(false);
        Invoke("timeout", PING_TIMEOUT);

        base.on_init_network_variables();
        color = new networked_variables.net_color();
        color.on_change = () =>
        {
            ui.GetComponent<UnityEngine.UI.Image>().color = color.value;
        };
    }

    public override void on_create()
    {
        base.on_create();

        // Let the ui know where I was created
        ui.pinged_position = transform.position;
        ui.gameObject.SetActive(true);
    }

    public override void on_first_create()
    {
        base.on_first_create();
        color.value = new Color(
            Random.Range(0, 1f),
            Random.Range(0, 1f),
            Random.Range(0, 1f)
        );
    }

    public override void on_forget(bool deleted)
    {
        // Destroy the UI when we're deleted
        base.on_forget(deleted);
        Destroy(ui.gameObject);
    }

    void timeout()
    {
        // Destroy this/disable the UI
        // (the ui will be destroyed also
        // once the delete() has processed)
        if (has_authority) delete();
        ui.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Make sure the ui knows where I am
        ui.pinged_position = transform.position;
    }

    public override float network_radius()
    {
        // Map pings are visible infinitely far away
        return Mathf.Infinity;
    }

    public override bool persistant()
    {
        // Map pings aren't saved
        return false;
    }
}