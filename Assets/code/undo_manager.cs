using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class undo_manager
{
    public const int MAX_UNDO_DEPTH = 10;

    // An undo returns a redo and vice versa. That's one clever compiler.
    public delegate undo_action undo_action();

    static List<undo_action> undo_levels = new List<undo_action>();
    static List<undo_action> redo_levels = new List<undo_action>();

    public static void register_undo_level(undo_action undo)
    {
        // Register an undo level
        undo_levels.Add(undo);
        while (undo_levels.Count > MAX_UNDO_DEPTH)
            undo_levels.RemoveAt(0);
    }

    static void register_redo_level(undo_action redo)
    {
        // Register a redo level
        redo_levels.Add(redo);
        while (redo_levels.Count > MAX_UNDO_DEPTH)
            redo_levels.RemoveAt(0);
    }

    public static bool undo()
    {
        // Carry out the most recent undo actions until one succeeds
        while (undo_levels.Count > 0)
        {
            var undo = undo_levels[undo_levels.Count - 1];
            undo_levels.RemoveAt(undo_levels.Count - 1);
            var redo = undo();

            if (redo != null)
            {
                // A successful undo, register the correspoding redo
                register_redo_level(redo);
                popup_message.create("Undo");
                return true;
            }
        }
        return false;
    }

    public static bool redo()
    {
        // Carry out the most recent redo actions until one succeeds
        while (redo_levels.Count > 0)
        {
            var redo = redo_levels[redo_levels.Count - 1];
            redo_levels.RemoveAt(redo_levels.Count - 1);
            var undo = redo();

            if (undo != null)
            {
                // A successful redo, register the correspoding undo
                register_undo_level(undo);
                popup_message.create("Redo");
                return true;
            }
        }
        return false;
    }
}
