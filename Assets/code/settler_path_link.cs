using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_path_link : MonoBehaviour
{
    public const float LINK_DISTANCE = 0.25f;

    public settler_path_link linked_to
    {
        get => _linked_to;
        set
        {
            _linked_to = value;

            // If we have a link, but no link display, create one
            if (_linked_to != null && display.transform.childCount == 0)
            {
                var elm = GetComponentInParent<settler_path_element>();
                var link = Resources.Load<GameObject>("misc/path_link").inst();
                Vector3 to = elm.transform.position - transform.position;
                link.transform.position = transform.position + to / 2f;
                link.transform.LookAt(elm.transform.position);
                link.transform.localScale = new Vector3(0.1f, 0.1f, to.magnitude);
                link.transform.SetParent(display.transform);
            }

            _display.GetComponent<Renderer>().material =
                _linked_to == null ?
                Resources.Load<Material>("materials/red") :
                Resources.Load<Material>("materials/green");

            // Destroy any link display
            if (_linked_to == null)
                foreach (Transform c in display.transform)
                    Destroy(c.gameObject);
        }
    }
    settler_path_link _linked_to;

    GameObject display
    {
        get
        {
            if (_display == null)
            {
                // Create the display sub-object
                _display = Resources.Load<GameObject>("misc/path_point").inst();
                _display.transform.SetParent(transform);
                _display.transform.localPosition = Vector3.zero;
                _display.transform.localRotation = Quaternion.identity;
                _display.transform.localScale = Vector3.one * LINK_DISTANCE;

                // Initial state depends on if drawing is already enabled
                _display.SetActive(settler_path_element.draw_links);
            }

            return _display;
        }
    }
    GameObject _display;

    public bool display_enabled
    {
        get => display.activeInHierarchy;
        set => display.SetActive(value);
    }

    private void Start()
    {
        // Don't do anything if this is equipped
        if (transform.GetComponentInParent<player>() != null)
            return;

        // Ensure that the display exists + is in the correct state
        if (display_enabled != settler_path_element.draw_links)
            throw new System.Exception("Display created incorrectly!");
    }

    private void OnDrawGizmosSelected()
    {
        if (linked_to == null)
            Gizmos.color = Color.red;
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, linked_to.transform.position);
        }
        Gizmos.DrawWireSphere(transform.position, LINK_DISTANCE / 2f);
    }
}
