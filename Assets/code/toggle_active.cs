using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class toggle_active : MonoBehaviour
{
    public void toggle_active_in_hierarchy()
    {
        gameObject.SetActive(!gameObject.activeInHierarchy);
    }
}
