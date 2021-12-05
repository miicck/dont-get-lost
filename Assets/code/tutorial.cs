using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class tutorial
{
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

                () => build_requirement.create("log_splitter", 1, advance_stage),

                () => confirm_window.create(
                    "Now you can use the log splitter to make some planks.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make some planks", new Dictionary<string, int>{ ["plank"] = 10 },
                    advance_stage),

                () => confirm_window.create(
                    "We're going to need some more advanced tools as well.\n" +
                    "For those, we are going to need some metal.\n" +
                    "To make the metal, we're going to need a furnace.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make a furnace.\n" +
                    "Check the recipe by pressing "+controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) + "\n" +
                    "The stone you need can be harvested from nearby rocks using the pickaxe.",
                    new Dictionary<string, int>{["furnace"] = 1},
                    advance_stage),

                () => confirm_window.create(
                    "Good job - you're getting the hang of this!\n" +
                    "Build the furnace somewhere so that we can make some metal.",
                    advance_stage),

                () => build_requirement.create(
                    "furnace", 1, advance_stage),

                () => item_requirement_tracker.create(
                    "Make some metal.\n" +
                    "There should be some brown-ish iron ore deposits nearby.\n" +
                    "Wood can be used as fuel.",
                    new Dictionary<string, int>{["iron"] = 10},
                    advance_stage),

                () => confirm_window.create(
                    "Now that we've got the iron, we need an anvil to\n" +
                    "make it into useful shapes. Use some of the iron\n" +
                    "to make an anvil.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make an anvil.",
                    new Dictionary<string, int>{["anvil"] = 1},
                    advance_stage),

                () => confirm_window.create(
                    "Great - put the anvil somewhere convinient so that we can use it.",
                    advance_stage),

                () => build_requirement.create("anvil", 1, advance_stage),

                () => confirm_window.create(
                    "Now, we're going to replace the log splitter with a more\n" +
                    "advanced piece of kit - a sawmill!\n" +
                    "We can use the anvil to make the sawblade.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make a sawblade",
                    new Dictionary<string, int>{["sawblade"] = 1},
                    advance_stage),

                () => confirm_window.create(
                    "You already know how to make the other ingredients\n" +
                    "for the sawmill - go ahead and make one.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make a sawmill",
                    new Dictionary<string, int>{["sawmill"] = 1},
                    advance_stage),

                () => build_requirement.create(
                    "sawmill", 1, advance_stage),

                () => confirm_window.create(
                    "Now we can start to build our camp. Using some planks, make " +
                    "some wooden path sections.",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make some wooden path sections (press "+
                    controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK)+ " for the recipe)",
                    new Dictionary<string, int>{["wooden_path"] = 10},
                    advance_stage),

                () => confirm_window.create(
                    "Good job. Now we're going to have to think about a basic camp layout.",
                    advance_stage),

                () => build_requirement.create("wooden_path", 6, advance_stage,
                    hint: "The path sections should be connected together.\n" +
                          "Click on an existing path to snap a new path to it.",
                    constraint: build_requirement_constraints.is_linked),

                ()=> confirm_window.create(
                    "Great, you should have seen a green path highlighted - this is\n" +
                    "the path that settlers can use to move around your camp.\n"+
                    "Eventually, a red path leading off into the distance will\n"+
                    "be generated - this is an entrypoint to your camp.",
                    advance_stage),

                () => confirm_window.create(
                    "For settlers to move in, they need somewhere to sleep.\n" +
                    "To save some time, I'm going to give you a bed - \n" +
                    "connect it to one of the paths.",
                    () =>
                    {
                        player.call_when_current_player_available(()=>player.current.inventory.add("bed", 1));
                        advance_stage();
                    }),

                () => build_requirement.create("bed", 1, advance_stage,
                    hint: "The bed should be connected to the town.\n" +
                          "To connect the bed to a path, click\n" +
                          "on the path when building the bed.",
                    constraint: build_requirement_constraints.is_linked),

                () => confirm_window.create(
                    "Good job. A settler will soon move into the camp.\n" +
                    "They are going to need some food to eat, so we should\n" +
                    "get to work building a farm!",
                    advance_stage),

                () => item_requirement_tracker.create(
                    "Make the components of a cabbage farm.\n" +
                    "A cabbage farm will consist of a cabbage planter\n" +
                    "that will drop cabbages onto some plank gutters\n" +
                    "that will feed the cabbages into a pantry",
                    new Dictionary<string, int> {
                        ["cabbage_planter"] = 1,
                        ["plank_gutter"] = 2,
                        ["pantry"] = 1
                    },
                    advance_stage),

                () => confirm_window.create(
                    "Now, lets kit our town out with a farm!\n" +
                    "While we're at it, we should build some basic defences\n." +
                    "A simple guard spot should be enough for now.",
                    advance_stage),

                () => basic_camp_requirement.create(
                    advance_stage),

                () => confirm_window.create(
                    "Congratulations - you've built your first functional camp,\n" +
                    "well on the way to a fully-fledged town!\n\n" +
                    "For here on, it's up to you to expand as you see fit\n" +
                    "useful tips can be found in the help book by pressing "+
                    controls.bind_name(controls.BIND.TOGGLE_HELP_BOOK)+".",
                    advance_stage)
            };
            return _tutorial_stages;
        }
    }
    static tutorial_object_generator[] _tutorial_stages;
    delegate tutorial_object tutorial_object_generator();

    public static void advance_stage() => player.call_when_current_player_available(() => player.current.advance_tutorial_stage());
    static tutorial_object tutorial_object;

    public static void set_stage(int stage)
    {
        if (tutorial_object != null) Object.Destroy(tutorial_object.gameObject);
        tutorial_object = null;
        if (stage < 0) return;
        if (stage >= tutorial_stages.Length) return;
        tutorial_object = tutorial_stages[stage]();

        player.current.force_interaction(tutorial_object.interaction);
    }
}

public abstract class tutorial_object : MonoBehaviour
{
    public player_interaction interaction
    {
        get
        {
            if (_interaction == null)
                _interaction = new tutorial_interaction(this);
            return _interaction;
        }
    }
    player_interaction _interaction;

    class tutorial_interaction : player_interaction
    {
        public override controls.BIND keybind => controls.BIND.FORCED_INTERACTION;
        public override string context_tip() => "";
        public override bool show_context_tip() => false;

        public tutorial_interaction(tutorial_object obj) => this.obj = obj;
        tutorial_object obj;

        public override bool simultaneous() => obj.allows_other_interactions();
        public override bool allows_movement() => obj.allows_movement();
        public override bool allows_mouse_look() => obj.allows_mouse_look();
        protected override bool on_start_interaction(player player) { obj.start_interaction(); return false; }
        public override bool continue_interaction(player player) => false;
        protected override void on_end_interaction(player player) => obj.end_interaction();
        protected override bool mouse_visible() => obj.mouse_visible();
    }

    protected virtual bool allows_other_interactions() => true;
    protected virtual bool allows_movement() => true;
    protected virtual bool allows_mouse_look() => true;
    protected virtual void start_interaction() { }
    protected virtual void end_interaction() { }
    protected virtual bool mouse_visible() => false;
}

class confirm_window : tutorial_object
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
            player.current.force_interaction(null);
            Destroy(ui.gameObject);
        });
        var ret = ui.gameObject.AddComponent<confirm_window>();
        ret.on_confirm = on_confirm;
        return ret;
    }

    protected override bool allows_mouse_look() => false;
    protected override bool allows_movement() => false;
    protected override bool allows_other_interactions() => false;

    protected override bool mouse_visible()
    {
        // Need a mouse to be able to click the button
        return true;
    }

    protected override void end_interaction()
    {
        // confirm window interaction ended => confirmation
        on_confirm?.Invoke();
    }
}

class basic_camp_requirement : tutorial_object
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
        if (player.current == null) return;
        var valid_entrypoints = attacker_entrypoint.valid_entrypoints(group: player.current.group);

        bool entrypoint = valid_entrypoints.Count > 0;
        bool path_to_bed = false;
        bool path_to_planter = false;
        bool path_to_pantry = false;
        bool path_to_guard_spot = false;
        bool planter_connected_to_pantry = false;

        food_dipsenser pantry = null;
        settler_field planter = null;

        if (player.current != null)
        {
            foreach (var ep in valid_entrypoints)
                ep?.element?.iterate_connected((e) =>
                {
                    if (e != null)
                    {
                        if (e.interactable is bed) path_to_bed = true;
                        if (e.interactable is guard_spot) path_to_guard_spot = true;
                        if (e.interactable is settler_field)
                        {
                            path_to_planter = true;
                            planter = (settler_field)e.interactable;
                        }
                        if (e.interactable is food_dipsenser)
                        {
                            path_to_pantry = true;
                            pantry = (food_dipsenser)e.interactable;
                        }
                    }
                    return path_to_bed && path_to_planter && path_to_pantry && path_to_guard_spot;
                });
        }

        if (planter != null && pantry != null)
        {
            planter.output.iterate_downstream((n) =>
            {
                if (n == pantry.item_dispenser.input)
                {
                    planter_connected_to_pantry = true;
                    return true;
                }
                return false;
            });
        }

        requirement_text.text =
                       (entrypoint ? "[x]" : "[ ]") + " entrypoint (EP) to town\n" +
                      (path_to_bed ? "[x]" : "[ ]") + " path from EP to bed\n" +
                  (path_to_planter ? "[x]" : "[ ]") + " path from EP to planter\n" +
                   (path_to_pantry ? "[x]" : "[ ]") + " path from EP to pantry\n" +
               (path_to_guard_spot ? "[x]" : "[ ]") + " path from EP to guard spot\n" +
      (planter_connected_to_pantry ? "[x]" : "[ ]") + " planter connected to pantry";

        if (!entrypoint) return;
        if (!path_to_bed) return;
        if (!path_to_planter) return;
        if (!path_to_pantry) return;
        if (!path_to_guard_spot) return;
        if (!planter_connected_to_pantry) return;

        Destroy(gameObject);
        on_finish?.Invoke();
    }
}