using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that will rotate constantly at 
/// the  given speed arond the given axis. </summary>
public class always_rotates : MonoBehaviour
{
    public float speed = 30;
    public AXIS axis;

    void Update()
    {
        float amt = -speed * Time.deltaTime;
        switch (axis)
        {
            case AXIS.X_AXIS:
                transform.Rotate(amt, 0, 0);
                break;

            case AXIS.Y_AXIS:
                transform.Rotate(0, amt, 0);
                break;

            case AXIS.Z_AXIS:
                transform.Rotate(0, 0, amt);
                break;
        }
    }
}
