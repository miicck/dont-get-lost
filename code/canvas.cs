using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class canvas
{
    static Canvas canv;
    static Text debug_info;

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
    }

    static string location_info()
    {
        return "No location info";
    }

    public static void update()
    {
        debug_info.text = "FPS: " + (1 / Time.deltaTime) + "\n";
        debug_info.text += location_info();
    }
}
