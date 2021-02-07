using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class confirm_window : MonoBehaviour
{
    public delegate void confirm_func();
    confirm_func on_confirm;

    public static confirm_window create(string message, confirm_func on_confirm = null)
    {
        var ui = Resources.Load<RectTransform>("ui/confirm_window").inst();
        ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        ui.anchoredPosition = Vector2.zero;
        ui.GetComponentInChildren<UnityEngine.UI.Text>().text = message;
        ui.GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(() =>
        {
            Destroy(ui.gameObject);
        });
        var ret = ui.gameObject.AddComponent<confirm_window>();
        ret.on_confirm = on_confirm;
        return ret;
    }

    private void Start()
    {
        player.call_when_current_player_available(() => player.current.cursor_sprite = null);
        controls.disabled = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        player.call_when_current_player_available(() => player.current.cursor_sprite = cursors.DEFAULT);
        controls.disabled = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        on_confirm?.Invoke();
    }
}

public static class tutorial
{
    static List<GameObject> tutorial_objects = new List<GameObject>();

    public static void advance_stage()
    {
        player.call_when_current_player_available(() => player.current.advance_tutorial_stage());
    }

    public static void set_stage(int stage)
    {
        foreach (var t in tutorial_objects)
            if (t != null)
                Object.Destroy(t);
        tutorial_objects = new List<GameObject>();

        switch (stage)
        {
            case 0:
                tutorial_objects.Add(confirm_window.create(
                    "Welcome to don't get lost!\n\n" +
                    "We have arrived in a strange new land, tasked with\n" +
                    "setting up new colonies to export natural resources.",
                    advance_stage).gameObject);
                break;

            case 1:
                tutorial_objects.Add(confirm_window.create(
                    "The first thing we need to do is set up a base camp.\n" +
                    "To do this, we're going to need some tools. See if you\n" +
                    "can find some materials to make some tools from...",
                    advance_stage).gameObject);
                break;

            case 2:
                tutorial_objects.Add(item_requirement_tracker.create(
                    "Find some materials to make tools.\n" +
                    "  - Flint can be found by scavenging on the floor\n" +
                    "  - Sticks can be found in the trees\n" +
                    "If you're confused about how to interact with objects,\n" +
                    "look in the bottom right of the screen for hints.",
                    new Dictionary<string, int>
                    {
                        ["flint"] = 2,
                        ["stick"] = 2
                    }, advance_stage).gameObject);
                break;

            case 3:
                tutorial_objects.Add(confirm_window.create(
                    "Great, it looks like you've got what you need.\n" +
                    "Go ahead and craft an axe and a pickaxe.",
                    advance_stage).gameObject);
                break;

            case 4:
                tutorial_objects.Add(item_requirement_tracker.create(
                    "Craft a flint axe and a flint pickxaxe.\n" +
                    "Your can do this from inside your inventory, which\n" +
                    "can be accessed by pressing " +
                    controls.bind_name(controls.BIND.OPEN_INVENTORY) + ".\n" +
                    "Move the sticks and flint into the crafting area on the\n" +
                    "right hand side and click on the crafting options that appear.",
                    new Dictionary<string, int>
                    {
                        ["flint_pickaxe"] = 1,
                        ["flint_axe"] = 1
                    }, advance_stage).gameObject);
                break;
        }
    }
}
