using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class turret : MonoBehaviour
{
    Transform target
    {
        get
        {
            if (_target == null)
                _target = null;
            return _target;
        }

        set
        {
            _target = value;
        }
    }
    Transform _target;

    void idle()
    {
        Vector3 euler = Quaternion.identity.eulerAngles;
        euler.y = 90f * Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2f / 4f);
        transform.localRotation = Quaternion.Euler(euler);
    }

    void attack()
    {
        transform.LookAt(target);
    }

    void Update()
    {
        if (target == null) idle();
        else attack();
    }
}
