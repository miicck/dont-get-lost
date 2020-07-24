using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railcart : MonoBehaviour
{
    railway rail_on;

    void Update()
    {
        Vector3 delta = transform.position - rail_on.transform.position;
        if (delta.magnitude > 0.6f)
            rail_on = rail_on.next;
        
        if (rail_on == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += rail_on.transform.forward * Time.deltaTime;
    }
}
