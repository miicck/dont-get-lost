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
        ui.transform.SetParent(game.canvas.transform);
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

class basic_camp_requirement : MonoBehaviour
{
    public delegate void finished_func();
    finished_func on_finish;
    UnityEngine.UI.Text requirement_text;

    public static basic_camp_requirement create(finished_func on_finish)
    {
        var rt = Resources.Load<RectTransform>("ui/basic_camp_requirement").inst();
        rt.SetParent(game.canvas.transform);
        rt.anchoredPosition = Vector3.zero;
        var bcr = rt.gameObject.AddComponent<basic_camp_requirement>();
        bcr.on_finish = on_finish;

        foreach (var t in rt.GetComponentsInChildren<UnityEngine.UI.Text>())
            if (t.name.Contains("requirement"))
            {
                bcr.requirement_text = t;
                break;
            }

        return bcr;
    }

    private void Update()
    {
        bool path_from_gate_to_bed = false;
        bool path_from_gate_to_planter = false;
        bool path_from_gate_to_pantry = false;
        bool planter_connected_to_pantry = false;

        item_dispenser pantry = null;
        settler_field planter = null;

        if (player.current != null)
        {
            var tg = town_gate.nearest_gate(player.current.transform.position);
            tg?.path_element?.iterate_connected((e) =>
            {
                if (e != null)
                {
                    if (e.interactable is bed) path_from_gate_to_bed = true;
                    if (e.interactable is settler_field)
                    {
                        path_from_gate_to_planter = true;
                        planter = (settler_field)e.interactable;
                    }
                    if (e.interactable is item_dispenser)
                    {
                        path_from_gate_to_pantry = true;
                        pantry = (item_dispenser)e.interactable;
                    }
                }
                return path_from_gate_to_bed && path_from_gate_to_planter && path_from_gate_to_pantry;
            });
        }

        if (planter != null && pantry != null)
        {
            planter.output.iterate_downstream((n) =>
            {
                if (n == pantry.input)
                {
                    planter_connected_to_pantry = true;
                    return true;
                }
                return false;
            });
        }

        requirement_text.text =
            (path_from_gate_to_bed ? "[x]" : "[ ]") + " path from gate to bed\n" +
        (path_from_gate_to_planter ? "[x]" : "[ ]") + " path from gate to planter\n" +
         (path_from_gate_to_pantry ? "[x]" : "[ ]") + " path from gate to pantry\n" +
      (planter_connected_to_pantry ? "[x]" : "[ ]") + " planter connected to pantry";

        if (!path_from_gate_to_bed) return;
        if (!path_from_gate_to_planter) return;
        if (!path_from_gate_to_pantry) return;
        if (!planter_connected_to_pantry) return;

        Destroy(gameObject);
        on_finish?.Invoke();
    }
}

public static class tutorial
{
    public static void advance_stage()
    {
        player.call_when_current_player_available(() => player.current.advance_tutorial_stage());
    }

    delegate Component tutorial_object_generator();
    static tutorial_object_generator[] tutorial_stages
    {
        get
        {
            if (_tutorial_stages != null) return _tutorial_stages;
            _tutorial_stages = new tutorial_object_generator[]
            {
                () => confirm_window.create(
                    "Welcome to don't get lost!\n\n" +
                    "We have arrived in a strange new land, tasked with\n" +
                    "setting up colonies to export natural resources.",
                    advance_stage),

                () => confirm_window.create(
                    "The first thing we need to do is set up a base camp.\n" +
                    "To do this, we're going to need some tools. See if you\n" +
                    "can find some materials to make some tools from...",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Find some materials to make tools.\n" +
                    "  - Flint can be found by scavenging on the floor\n" +
                    "  - Sticks can be found in the trees\n" +
                    "If you're confused about how to interact with objects,\n" +
                    "look in the bottom right of the screen for hints.",
                    new Dictionary<string, int>
                    {
                        ["flint"] = 2,
                        ["stick"] = 2
                    }, advance_stage),

                () => confirm_window.create(
                    "Great, it looks like you've got what you need.\n" +
                    "Go ahead and craft an axe and a pickaxe.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Craft a flint axe and a flint pickaxe.\n" +
                    "Your can do this from inside your inventory,\n" +
                    "which can be accessed by pressing " +
                    controls.bind_name(controls.BIND.OPEN_INVENTORY) + ".\n" +
                    "Move the sticks and flint into the crafting area on the\n" +
                    "right hand side and click on the crafting options that appear.",
                    new Dictionary<string, int> {["flint_pickaxe"] = 1, ["flint_axe"] = 1}, advance_stage),

                () => confirm_window.create(
                    "Good job. Now we can start to put our camp together.\n" +
                    "Go ahead and collect some wood using your axe.",
                    advance_stage),

                () => equipped_item_requirement.create(
                    Resources.Load<item>("items/flint_axe"),
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Collect some wood. With the axe equipped, \n" +
                    "press " + controls.bind_name(controls.BIND.USE_ITEM) + " to swing it at a tree.",
                    new Dictionary<string, int> {["log"] = 10}, advance_stage),

                () => confirm_window.create(
                    "Good job. Now, we're going to need to split these logs into more\n" +
                    "useful shapes. Go ahead and craft a log splitter.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Craft a log splitter.\n" +
                    "To figure out what ingredients you need to put in\n" +
                    "the crafting slots, press " + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) +
                    " to open the recipe book \n" +
                    "and search for 'log splitter' in the top right.\n" +
                    "Press enter to finish searching and " + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) +
                    "\nto close the recipe book again.",
                    new Dictionary<string, int> {["log_splitter"] = 1}, advance_stage),

                () => confirm_window.create(
                    "Good job. In order to build the log splitter, equip it the same way you did with the axe.",
                    advance_stage),

                () => build_requirement.create("log_splitter", advance_stage),

                () => confirm_window.create(
                    "Now you can use the log splitter to make some planks.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make some planks.", new Dictionary<string, int>
                    {
                        ["plank"] = 20
                    }, ()=>{
                        player.call_when_current_player_available(() =>
                        {
                            player.current.inventory.remove("plank", 20);
                            player.current.inventory.add("wooden_town_gate", 1);
                            player.current.inventory.add("stone_path", 10);
                            player.current.inventory.add("pantry", 1);
                            player.current.inventory.add("plank_gutter", 5);
                            player.current.inventory.add("bed", 1);
                            player.current.inventory.add("cabbage_planter", 1);
                        });
                        advance_stage();
                    }),

                () => confirm_window.create("You've been given some materials to set up a camp.", advance_stage),

                () => basic_camp_requirement.create(advance_stage),

                () => confirm_window.create(
                    "Great job - now we've got a base camp set up, it might\n"+
                    "be a good idea to go explore for a nice place to set up\n"+
                    "our first outpost. But it's up to you from here, good luck!", advance_stage)
            };
            return _tutorial_stages;
        }
    }
    static tutorial_object_generator[] _tutorial_stages;

    static Component tutorial_object;

    public static void set_stage(int stage)
    {
        if (tutorial_object != null) Object.Destroy(tutorial_object.gameObject);
        tutorial_object = null;
        if (stage < 0) return;
        if (stage >= tutorial_stages.Length) return;
        tutorial_object = tutorial_stages[stage]();
    }
}