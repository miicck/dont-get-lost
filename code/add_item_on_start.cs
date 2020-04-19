using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class add_item_on_start : MonoBehaviour
{
    public string item;
    public int count;
    public inventory inventory;
    void Start()
    {
        inventory.add(item, count);
    }
}
