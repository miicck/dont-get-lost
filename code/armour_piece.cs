using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class armour_piece : item
{ 
    public LOCATION location;
    public HANDEDNESS handedness = HANDEDNESS.EITHER;

    public Vector3 size = Vector3.one;

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
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
