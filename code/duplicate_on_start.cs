using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class duplicate_on_start : MonoBehaviour
{
    public int count_including_original = 1;
    public GameObject to_duplicate;

    void Start()
    {
        if (to_duplicate != null)
            for (int i = 0; i < count_including_original - 1; ++i)
            {
                var d = to_duplicate.inst();
                Destroy(d.GetComponent<duplicate_on_start>());
                d.transform.SetParent(to_duplicate.transform.parent);
                d.transform.localPosition = to_duplicate.transform.localPosition;
                d.transform.localRotation = to_duplicate.transform.localRotation;
            }

        Destroy(this);
    }
}
