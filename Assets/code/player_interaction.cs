using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that provides player interactions. </summary>
public interface IPlayerInteractable
{
    public player_interaction[] player_interactions(RaycastHit hit);
}

/// <summary> An interaction that a player can be carry out. </summary>
public abstract class player_interaction
{
    /// <summary> Returns the keybind needed to start this interaction </summary>
    public abstract controls.BIND keybind { get; }

    /// <summary> Returns true if the associated <see cref="keybind"/> can be held down. </summary>
    public virtual bool allow_held => false;

    /// <summary> Returns true if this interaction is triggered 
    /// based on <see cref="keybind"/> and <see cref="allow_held"/> </summary>
    public virtual bool triggered(player player)
    {
        // Don't start interactions on non-authority clients
        // (if you want networked interactions see networked_player_interaction)
        if (!player.has_authority) return false;
        return allow_held ? controls.held(keybind) : controls.triggered(keybind);
    }

    /// <summary> Returns false if the interaction is (temporarily) impossible. </summary>
    public virtual bool is_possible() { return true; }

    /// <summary> Returns true if the mouse should be visible during this interaction. </summary>
    protected virtual bool mouse_visible() { return false; }

    /// <summary> Shown at the bottom right of the screen to let the player 
    /// know what interactions are currently possible. </summary>
    public abstract string context_tip();

    /// <summary> Returns true if the context tip should be displayed. </summary>
    public virtual bool show_context_tip() { return true; }

    /// <summary> Called when an interaction starts, returns 
    /// true if the interaction is immediately completed. </summary>
    public bool start_interaction(player player)
    {
        if (player == player.current)
        {
            // Update cursor visibility state
            Cursor.visible = mouse_visible();
            Cursor.lockState = mouse_visible() ? CursorLockMode.None : CursorLockMode.Locked;
            if (mouse_visible()) player.cursor_sprite = null;
        }

        return on_start_interaction(player);
    }

    /// <summary> Called when an interaction starts, should return 
    /// true if the interaction is immediately completed. </summary>
    protected virtual bool on_start_interaction(player player) { return false; }

    /// <summary> Called once per frame when the interaction is underway,
    /// should return true once the interaction is over. </summary>
    public virtual bool continue_interaction(player player) { return true; }

    /// <summary> Called to end an interaction. </summary>
    public void end_interaction(player player)
    {
        if (player == player.current)
        {
            // Return to default invisible cursor
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            if (mouse_visible()) player.current.cursor_sprite = cursors.DEFAULT;
        }

        on_end_interaction(player);
    }

    /// <summary> Called when the interaction is completed. </summary>
    protected virtual void on_end_interaction(player player) { }

    /// <summary> An inventory that can be edited when 
    /// interacting with this. null otherwise. </summary>
    public virtual inventory editable_inventory() { return null; }

    /// <summary> Additioanl recipes available to the player when 
    /// interacting with this. </summary>
    public virtual recipe[] additional_recipes(
        out string name,
        out AudioClip crafting_sound,
        out float crafting_sound_vol)
    {
        name = null;
        crafting_sound = null;
        crafting_sound_vol = 1f;
        return null;
    }

    /// <summary> Can the player move whilst carrying out this interaction? </summary>
    public virtual bool allows_movement() { return true; }

    /// <summary> Can the player look around whilst carrying out this interaction? </summary>
    public virtual bool allows_mouse_look() { return true; }

    /// <summary> Returns true if this interaction can be carried out 
    /// simultaneously with other interactions. Use with caution. </summary>
    public virtual bool simultaneous() { return false; }
}

/// <summary> An interaction that is mirrored on remote clients. </summary>
public abstract class networked_player_interaction : player_interaction
{
    public override bool triggered(player player)
    {
        // Networked interactions are triggered normally on authority clients
        // and by player.networked_interaction_underway on non-auth clients
        if (player.has_authority) return base.triggered(player);
        return player.networked_interaction_underway(keybind);
    }

    public sealed override bool simultaneous()
    {
        // Networked interactions can't be simulaneous, by virtue of the
        // fact that they are triggered by the state of a *single* 
        // networked variable within the player
        return false;
    }

    protected sealed override bool on_start_interaction(player player)
    {
        // On the authority client, record the fact that
        // we've started this interaction
        if (player.has_authority) player.start_networked_interaction(keybind);
        return start_networked_interaction(player); // Start interaction (on all clients)
    }

    public virtual bool start_networked_interaction(player player) { return false; }

    public sealed override bool continue_interaction(player player)
    {
        // Continue the interaction on all clients, which is considered
        // complete when continue_networked_interaction returns true, or
        // when a non-auth client is no longer triggered.
        var complete = continue_networked_interaction(player);
        if (!player.has_authority && !triggered(player)) complete = true;
        return complete;
    }

    public virtual bool continue_networked_interaction(player player) { return true; }

    protected sealed override void on_end_interaction(player player)
    {
        // On the authority client, record the fact that 
        // we've ended this interaction
        if (player.has_authority) player.end_networked_interaction(keybind);
        end_networked_interaction(player); // End interaction (on all clients)
    }

    public virtual void end_networked_interaction(player player) { }
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

    /// <summary> Stops all underway tasks. </summary>
    void clear_underway(player player)
    {
        var to_end = new List<player_interaction>();
        foreach (var kv in underway) to_end.Add(kv.Value.interaction);
        underway.Clear();

        // End interactions *after* clearing underway (in case
        // end_interaction starts new iteractions)
        foreach (var i in to_end) i.end_interaction(player);
    }

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
    public recipe[] additional_recipes(out string name, out AudioClip crafting_sound, out float crafting_sound_vol)
    {
        recipe[] ret = null;
        name = null;
        crafting_sound = null;
        crafting_sound_vol = 1f;

        foreach (var kv in underway)
        {
            var recs = kv.Value.interaction.additional_recipes(out string iname, out AudioClip ics, out float vol);
            if (recs == null || recs.Length == 0) continue;
            name += iname + " ";
            if (ics != null)
            {
                crafting_sound = ics;
                crafting_sound_vol = vol;
            }

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
            ret += "    " + i.Value.interaction.GetType().FullName + "\n";
        return ret.TrimEnd();
    }

    /// <summary> Given a set of interactions, work out which are compatible
    /// with this interaction_set, start/add them if they have been triggered. </summary>
    public void add_and_start_compatible(IEnumerable<player_interaction> interactions,
        player player, bool update_context_info = false)
    {
        // Get the possible interactions with unique keybinds
        var unique_interactions = new Dictionary<controls.control, player_interaction>();
        foreach (var i in interactions)
        {
            var c = controls.current_control(i.keybind);
            if (unique_interactions.ContainsKey(c)) continue;
            if (!i.is_possible()) continue;
            unique_interactions[c] = i;
        }

        // Reset context tip
        string new_context_tip = "";

        // Consider unique interactions for addition
        foreach (var kv in unique_interactions)
        {
            var i = kv.Value;

            if (update_context_info && i.show_context_tip())
            {
                string ct = i.context_tip()?.Trim();
                if (ct != null && ct.Length > 0)
                {
                    if (i.allow_held) ct = "[hold " + kv.Key.name() + "] " + ct;
                    else ct = "[" + kv.Key.name() + "] " + ct;
                    new_context_tip += "\n" + ct;
                }
            }

            if (underway.ContainsKey(i.keybind)) continue;
            if (!i.simultaneous() && !simultaneous()) continue;
            if (!i.triggered(player)) continue;

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

        if (update_context_info && tips.context_tip != new_context_tip)
            tips.context_tip = new_context_tip;
    }

    /// <summary> Continue underway interactions </summary>
    public void continue_underway(player player, bool force_stop = false)
    {
        // Continue underway interactions
        foreach (var kv in new Dictionary<controls.BIND, started_info>(underway))
        {
            if (!force_stop)
            {
                // Don't continue interactions on same frame that they were started
                if (Time.frameCount <= kv.Value.frame_started) continue;

                // Continue underway interactions
                if (!kv.Value.interaction.continue_interaction(player)) continue;
            }

            // End finished interaction
            kv.Value.interaction.end_interaction(player);
            underway.Remove(kv.Key);
        }

        if (force_stop)
            tips.context_tip = "";
    }

    public void force_interaction(player p, player_interaction i)
    {
        clear_underway(p);
        if (i == null) return;

        underway[i.keybind] = new started_info
        {
            interaction = i,
            frame_started = Time.frameCount
        };

        if (i.start_interaction(p))
            underway.Remove(i.keybind); // Completed immediately
    }
}

/// <summary> A menu that appears alongside the inventory, to the left. 
/// If the menu created == null, then this will just open the inventory, but
/// potentially with the additional recipes given. </summary>
public abstract class left_player_menu : player_interaction
{
    string name;
    bool close_requested;
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

    protected override bool on_start_interaction(player player)
    {
        close_requested = false;

        if (menu != null)
        {
            // Position the left menu at the left_expansion_point but leave 
            // it parented to the canvas, rather than the player inventory
            var attach_point = player.inventory.ui.GetComponentInChildren<left_menu_attach_point>();
            menu.gameObject.SetActive(true);
            menu.SetParent(attach_point.transform);
            menu.anchoredPosition = Vector2.zero;
            menu.SetParent(game.canvas.transform);
            on_open();
        }

        inventory_opener.start_interaction(player);
        return false;
    }

    public override bool continue_interaction(player player)
    {
        // Left player menus close with the inventory.
        return close_requested || inventory_opener.continue_interaction(player);
    }

    protected override void on_end_interaction(player player)
    {
        if (menu != null)
        {
            menu.gameObject.SetActive(false);
            on_close();
        }

        inventory_opener.end_interaction(player);
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

    protected void close() { close_requested = true; }

    // IMPLEMENTATION //
    abstract protected RectTransform create_menu();
    protected virtual void on_open() { }
    protected virtual void on_close() { }
}