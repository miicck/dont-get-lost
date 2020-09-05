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
        base.on_init_network_variables();
        color = new networked_variables.net_color();
        color.on_change = () =>
        {
            if (ui != null)
                ui.GetComponent<UnityEngine.UI.Image>().color = color.value;
        };
    }

    public override void on_create()
    {
        base.on_create();

        // Create the ui ping thing
        ui = Resources.Load<ping_indicator>("ui/ping_indicator").inst();
        ui.pinged_position = transform.position;
        ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
    }

    public override void on_gain_authority()
    {
        base.on_gain_authority();

        color.value = new Color(
            Random.Range(0, 1f),
            Random.Range(0, 1f),
            Random.Range(0, 1f)
        );

        Invoke("timeout", PING_TIMEOUT);
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

    public override float network_radius()
    {
        // Map pings are visible infinitely far away
        return Mathf.Infinity;
    }
}