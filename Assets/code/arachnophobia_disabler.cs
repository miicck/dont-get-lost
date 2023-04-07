using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class arachnophobia_disabler : MonoBehaviour
{
    void Start()
    {
        if (options_menu.get_bool("arachnophobia_mode"))
        {
            gameObject.SetActive(false);
            Destroy(gameObject); // DESTROY the spiders
        }
    }
}
