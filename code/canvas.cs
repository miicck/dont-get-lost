using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class cursors
{
    public const string DEFAULT = "default_cursor";
    public const string DEFAULT_INTERACTION = "default_interact_cursor";
    public const string GRAB_OPEN = "default_interact_cursor";
    public const string GRAB_CLOSED = "grab_closed_cursor";
}

public static class canvas
{
    static Canvas canv;
    static Text debug_info;
    static Image crosshairs;
    static Image direction_indicator;
    public static Transform transform { get { return canv.transform; } }

    public static string cursor
    {
        get
        {
            if (crosshairs.sprite == null) return null;
            return crosshairs.sprite.name;
        }
        set
        {
            if (cursor == value) return;
            crosshairs.sprite = Resources.Load<Sprite>("sprites/" + value);
        }
    }

    public static void create()
    {
        // Create the canvas element
        canv = new GameObject("canvas").AddComponent<Canvas>();
        canv.renderMode = RenderMode.ScreenSpaceOverlay;
        canv.gameObject.AddComponent<CanvasScaler>();
        canv.gameObject.AddComponent<GraphicRaycaster>();
        canv.gameObject.AddComponent<EventSystem>();

        // Create the upper left debug info panel
        debug_info = new GameObject("debug_info").AddComponent<Text>();
        debug_info.font = Resources.Load<Font>("fonts/monospace");
        debug_info.transform.SetParent(canv.transform);
        debug_info.horizontalOverflow = HorizontalWrapMode.Overflow;
        debug_info.verticalOverflow = VerticalWrapMode.Overflow;

        var dirt = debug_info.GetComponent<RectTransform>();
        dirt.pivot = new Vector2(0, 1);
        dirt.anchorMin = new Vector2(0, 1);
        dirt.anchorMax = new Vector2(0, 1);
        dirt.anchoredPosition = new Vector2(0, 0);
        dirt.localPosition += new Vector3(5f, -5f, 0f);
        debug_info.text = "Debug info";
        debug_info.color = Color.black;

        // Create the crosshairs
        crosshairs = new GameObject("corsshairs").AddComponent<Image>();
        crosshairs.transform.SetParent(canv.transform);
        crosshairs.color = new Color(1, 1, 1, 0.5f);
        var crt = crosshairs.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(64, 64);
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        cursor = "default_cursor";

        // Create the direction indicator
        direction_indicator = new GameObject("direction_indicator").AddComponent<Image>();
        direction_indicator.transform.SetParent(canv.transform);
        var drt = direction_indicator.GetComponent<RectTransform>();
        drt.sizeDelta = new Vector2(64, 64);
        drt.anchorMin = new Vector2(0.5f, 0.5f);
        drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = Vector2.zero;
        direction_indicator.sprite = Resources.Load<Sprite>("sprites/direction_indicator");
    }

    static string location_info()
    {
        int xworld = (int)game.player.transform.position.x;
        int zworld = (int)game.player.transform.position.z;
        string ret = "Biome mix: " + world.biome_mix_info(xworld, zworld) + "\n";
        ret += "Point info: " + world.point(xworld, zworld).info();
        return ret;
    }

    public static void update()
    {
        debug_info.text = "FPS: " + (1 / Time.deltaTime) + "\n";
        debug_info.text += "Render range: " + game.render_range + "\n";
        debug_info.text += location_info() + "\n";
    }

    public static void set_direction_indicator(Vector2 direction)
    {
        direction_indicator.transform.localRotation =
            Quaternion.LookRotation(Vector3.forward, direction);
        direction_indicator.transform.localScale = new Vector3(
            1, direction.magnitude, 1);
    }
}

public class popup_message : MonoBehaviour
{
    // The scroll speed of a popup message
    // (in units of the screen height)
    public const float SCREEN_SPEED = 0.05f;

    new RectTransform transform;
    Text text;
    float start_time;

    public static popup_message create(string message)
    {
        var m = new GameObject("message").AddComponent<popup_message>();
        m.text = m.gameObject.AddComponent<Text>();

        m.transform = m.GetComponent<RectTransform>();
        m.transform.SetParent(canvas.transform);
        m.transform.anchorMin = new Vector2(0.5f, 0.25f);
        m.transform.anchorMax = new Vector2(0.5f, 0.25f);
        m.transform.anchoredPosition = Vector2.zero;

        m.text.font = Resources.Load<Font>("fonts/monospace");
        m.text.text = message;
        m.text.alignment = TextAnchor.MiddleCenter;
        m.text.verticalOverflow = VerticalWrapMode.Overflow;
        m.text.horizontalOverflow = HorizontalWrapMode.Overflow;
        m.text.fontSize = 32;
        m.start_time = Time.realtimeSinceStartup;

        return m;
    }

    private void Update()
    {
        transform.position +=
            Vector3.up * Screen.height *
            SCREEN_SPEED * Time.deltaTime;

        float time = Time.realtimeSinceStartup - start_time;

        text.color = new Color(1, 1, 1, 1 - time);

        if (time > 1)
            Destroy(this.gameObject);
    }
}