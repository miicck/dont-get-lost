using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that provides player interactions. </summary>
public interface IPlayerInteractable
{
    public player_interaction[] player_interactions();
}

/// <summary> An object that a player can be interacted with by the player. </summary>
public abstract class player_interaction
{
    /// <summary> Returns the keybind needed to start this interaction </summary>
    public abstract controls.BIND keybind { get; }

    /// <summary> Returns true if the associated <see cref="keybind"/> can be held down. </summary>
    public virtual bool allow_held => false;

    /// <summary> Returns true if this interaction is triggered 
    /// based on <see cref="keybind"/> and <see cref="allow_held"/> </summary>
    public bool triggered() { return allow_held ? controls.held(keybind) : controls.triggered(keybind); }

    /// <summary> Returns false if the interaction is (temporarily) impossible. </summary>
    public virtual bool is_possible() { return true; }

    /// <summary> Shown at the bottom right of the screen to let the player 
    /// know what interactions are currently possible. </summary>
    public abstract string context_tip();

    /// <summary> Returns true if the context tip should be displayed. </summary>
    public virtual bool show_context_tip() { return true; }

    /// <summary> Called when an interaction starts, should return 
    /// true if the interaction is immediately completed. </summary>
    public virtual bool start_interaction(player player) { return false; }

    /// <summary> Called once per frame when the interaction is underway,
    /// should return true once the interaction is over. </summary>
    public virtual bool continue_interaction(player player) { return true; }

    /// <summary> Called when the interaction is completed. </summary>
    public virtual void end_interaction(player player) { }

    /// <summary> An inventory that can be edited when 
    /// interacting with this. null otherwise. </summary>
    public virtual inventory editable_inventory() { return null; }

    /// <summary> Additioanl recipes available to the player when 
    /// interacting with this. </summary>
    public virtual recipe[] additional_recipes(out string name) { name = null; return null; }

    /// <summary> Can the player move whilst carrying out this interaction? </summary>
    public virtual bool allows_movement() { return true; }

    /// <summary> Can the player look around whilst carrying out this interaction? </summary>
    public virtual bool allows_mouse_look() { return true; }

    /// <summary> Returns true if this interaction can be carried out 
    /// simultaneously with other interactions. Use with caution. </summary>
    public virtual bool simultaneous() { return false; }
}

/// <summary> A set of interactions that can be carried out simultaneously. </summary>
public class interaction_set
{
    struct started_info
    {
        public player_interaction interaction;
        public int frame_started;
    }

    /// <summary> Interactions that are currently underway. </summary>
    Dictionary<controls.BIND, started_info> underway = new Dictionary<controls.BIND, started_info>();
    public int underway_count => underway.Count;

    int last_frame_completed_interaction = -1;

    /// <summary> Returns true if this interaction set can be
    /// carried out alongside other interactions. </summary>
    public bool simultaneous()
    {
        foreach (var kv in underway)
            if (!kv.Value.interaction.simultaneous())
                return false;
        return true;
    }

    /// <summary> Returns false if any of the interactions 
    /// currently underway disallow movement. </summary>
    public bool movement_allowed
    {
        get
        {
            foreach (var kv in underway)
                if (!kv.Value.interaction.allows_movement())
                    return false;
            return true;
        }
    }

    /// <summary> Returns false if any of the interactions 
    /// currently underway disallow mouse look. </summary>
    public bool mouse_look_allowed
    {
        get
        {
            foreach (var kv in underway)
                if (!kv.Value.interaction.allows_mouse_look())
                    return false;
            return true;
        }
    }

    /// <summary> Returns the first editable inventory that 
    /// we're interacting with. </summary>
    public inventory editable_inventory()
    {
        foreach (var kv in underway)
        {
            var inv = kv.Value.interaction.editable_inventory();
            if (inv != null) return inv;
        }
        return null;
    }

    /// <summary> Gets all of the additional recipes due
    /// to currently underway interactions. </summary>
    public recipe[] additional_recipes(out string name)
    {
        recipe[] ret = null;
        name = null;

        foreach (var kv in underway)
        {
            var recs = kv.Value.interaction.additional_recipes(out string iname);
            if (recs == null || recs.Length == 0) continue;
            name += iname + " ";

            if (ret == null) ret = recs;
            else ret = ret.append(recs);
        }

        return ret;
    }

    /// <summary> Get information about the current interactions. </summary>
    public string info()
    {
        string ret = "    " + underway_count + " interaction(s) underway\n";
        foreach (var i in underway)
            ret += "    " + i.GetType().FullName + "\n";
        return ret.TrimEnd();
    }

    /// <summary> Given a set of interactions, work out which are compatible
    /// with this interaction_set, start/add them if they have been triggered. </summary>
    public void add_and_start_compatible(IEnumerable<player_interaction> interactions,
        player player, bool update_context_info = false)
    {
        // Get the possible interactions with unique keybinds
        var unique_interactions = new Dictionary<controls.BIND, player_interaction>();
        foreach (var i in interactions)
        {
            if (unique_interactions.ContainsKey(i.keybind)) continue;
            if (!i.is_possible()) continue;
            unique_interactions[i.keybind] = i;
        }

        // Reset context tip
        if (update_context_info) tips.context_tip = "";

        // Consider unique interactions for addition
        foreach (var kv in unique_interactions)
        {
            var i = kv.Value;

            if (update_context_info && i.show_context_tip())
            {
                string ct = i.context_tip()?.Trim();
                if (ct != null && ct.Length > 0)
                {
                    if (i.allow_held) ct = "[hold " + controls.bind_name(i.keybind) + "] " + ct;
                    else ct = "[" + controls.bind_name(i.keybind) + "] " + ct;
                    tips.context_tip += "\n" + ct;
                }
            }

            if (underway.ContainsKey(i.keybind)) continue;
            if (!i.simultaneous() && !simultaneous()) continue;
            if (!i.triggered()) continue;

            // Add to underway tasks. We need to do this before we call 
            // start_interaction, in case start_interaction queries 
            // currently underway interactions (for example, when the player
            // opens their inventory, player.current_interactions are checked
            // for additional recipes)
            underway[i.keybind] = new started_info
            {
                interaction = i,
                frame_started = Time.frameCount
            };

            if (i.start_interaction(player))
                underway.Remove(i.keybind); // Immediately completed, remove from underway
        }
    }

    /// <summary> Continue underway interactions </summary>
    public void continue_underway(player player)
    {
        // Continue underway interactions
        foreach (var kv in new Dictionary<controls.BIND, started_info>(underway))
        {
            // Don't continue interactions on same frame that they were started
            if (Time.frameCount <= kv.Value.frame_started) continue;

            // Don't end interactions that haven't finished yet
            if (!kv.Value.interaction.continue_interaction(player)) continue;

            // End finished interaction
            kv.Value.interaction.end_interaction(player);
            underway.Remove(kv.Key);
            last_frame_completed_interaction = Time.frameCount;
        }
    }
}

/// <summary> A menu that appears alongside the inventory, to the left. </summary>
public abstract class left_player_menu : player_interaction
{
    string name;
    player.inventory_interaction inventory_opener;

    public left_player_menu(string name)
    {
        this.name = name;
        inventory_opener = new player.inventory_interaction();
    }

    // Left player menus open with the inventory.
    public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
    public override string context_tip() { return "interact with " + name; }
    public override bool allows_movement() { return false; }
    public override bool allows_mouse_look() { return false; }

    public override bool start_interaction(player player)
    {
        if (menu == null)
            return true; // Menu generation failed

        // Position the left menu at the left_expansion_point but leave 
        // it parented to the canvas, rather than the player inventory
        var attach_point = player.inventory.ui.GetComponentInChildren<left_menu_attach_point>();
        menu.gameObject.SetActive(true);
        menu.SetParent(attach_point.transform);
        menu.anchoredPosition = Vector2.zero;
        menu.SetParent(Object.FindObjectOfType<game>().main_canvas.transform);

        on_open();
        inventory_opener.start_interaction(player);
        return false;
    }

    public override bool continue_interaction(player player)
    {
        // Left player menus close with the inventory.
        return inventory_opener.continue_interaction(player);
    }

    public override void end_interaction(player player)
    {
        menu.gameObject.SetActive(false);
        inventory_opener.end_interaction(player);
        on_close();
    }

    protected RectTransform menu
    {
        get
        {
            if (_menu == null) _menu = create_menu();
            return _menu;
        }
    }
    RectTransform _menu;

    // IMPLEMENTATION //
    abstract protected RectTransform create_menu();
    protected virtual void on_open() { }
    protected virtual void on_close() { }
}