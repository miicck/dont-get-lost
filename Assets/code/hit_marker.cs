using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hit_marker : MonoBehaviour
{
    RectTransform ui;
    float time_alive;
    float timeout;

    public static hit_marker create(string text, Sprite image = null, float timeout = 2f)
    {
        var ret = new GameObject("hit_marker").AddComponent<hit_marker>();
        ret.ui = Resources.Load<RectTransform>("ui/hit_marker").inst();
        ret.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = text;
        var img = ret.ui.GetComponentInChildren<UnityEngine.UI.Image>();
        if (image == null) img.enabled = false;
        else img.sprite = image;
        ret.ui.transform.SetParent(game.canvas.transform);
        ret.timeout = timeout;
        return ret;
    }

    private void Update()
    {
        time_alive += Time.deltaTime;
        if (time_alive > timeout)
        {
            Destroy(gameObject);
            Destroy(ui.gameObject);
            return;
        }

        if (player.current == null) return;
        ui.position = utils.clamped_screen_point(player.current.camera, transform.position, out bool on_edge) +
            Vector3.up * time_alive * 10;

        ui.gameObject.SetActive(!on_edge);
    }
}