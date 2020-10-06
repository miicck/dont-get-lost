using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scavange_timer : MonoBehaviour
{
    public scavangable scavanging;
    public RectTransform green;

    float max_width;
    
    private void Start()
    {
        max_width = green.sizeDelta.x;
        green.sizeDelta = new Vector2(0, green.sizeDelta.y);
    }

    void Update()
    {
        float new_width = green.sizeDelta.x;
        new_width += Time.deltaTime * max_width;

        if (new_width > max_width)
        {
            scavanging.complete_scavange();
            Destroy(gameObject);
        }

        green.sizeDelta = new Vector2(new_width, green.sizeDelta.y);
    }
}