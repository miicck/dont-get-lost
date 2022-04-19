using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An item that can be worn. </summary>
public class armour_piece : item
{ 
    /// <summary> The location on the body where this armour piece is worn. </summary>
    public LOCATION location;

    /// <summary> The side of the body that this armour pice is worn on. Armour
    /// pieces with handedness set to EITHER will be mirrored when equipped on
    /// the left hand side. </summary>
    public HANDEDNESS handedness = HANDEDNESS.EITHER;

    public Vector3 size = Vector3.one;

    /// <summary> Set to true if a settler can be generated already wearing this item. </summary>
    public bool settler_can_generate_with = false;

    public enum LOCATION
    {
        HEAD,
        NECK,
        SHOULDER,
        FOREARM,
        CHEST,
        HAND,
        WAIST,
        THIGH,
        SHIN,
        FOOT
    };

    public enum HANDEDNESS
    {
        EITHER,
        LEFT,
        RIGHT
    };

    /// <summary> Returns true if a slot with handedness <paramref name="slot_handedness"/> can contain
    /// a piece of armour with handednesss <paramref name="piece_handedness"/>. </summary>
    public static bool compatible_handedness(HANDEDNESS slot_handedness, HANDEDNESS piece_handedness)
    {
        switch (piece_handedness)
        {
            case HANDEDNESS.EITHER:
                return true;

            case HANDEDNESS.LEFT:
                return slot_handedness == HANDEDNESS.LEFT ||
                       slot_handedness == HANDEDNESS.EITHER;

            case HANDEDNESS.RIGHT:
                return slot_handedness == HANDEDNESS.RIGHT ||
                       slot_handedness == HANDEDNESS.EITHER;

            default:
                throw new System.Exception("Unkown armour handedness!");
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, size);
    }

    public virtual void on_equip(ICanEquipArmour entity) { }
}
