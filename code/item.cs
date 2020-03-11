using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : MonoBehaviour
{
    public const float COLLECTION_DRAG_DISTANCE = 4f;
    public const float COLLECTION_DISTANCE = 1f;
    public const float MAX_COLLECTION_SPEED = 4f;
    public const float SPIN_SPEED = 30f;
    public const float SPIN_SPEED_RAND = 20f;
    public const float ANCHOR_OUTWARD_SEARCH = 0.1f;
    public const float ANCHOR_INWARD_SEARCH = ANCHOR_OUTWARD_SEARCH * 2;

    new public Rigidbody rigidbody { get; private set; }

    // The pivot used to manipulate this item
    Transform _pivot;
    public Transform pivot
    {
        get
        {
            if (_pivot == null)
            {
                // Create pivot if it doesn't exist
                _pivot = new GameObject("anchor").transform;
                _pivot.SetParent(transform);
                _pivot.transform.localPosition = Vector3.zero;
                _pivot.transform.localRotation = Quaternion.identity;
            }
            return _pivot;
        }
    }

    public void set_pivot(Vector3 world_position, Vector3 normal)
    {
        // Set the position and normal as requested
        pivot.position = world_position;
        pivot.forward = normal;
    }

    public void anchor_at(Vector3 position, Vector3 normal)
    {
        // Anchor this item so that it's anchor normal is opposite to 
        // the given normal anchor position is at the given position
        var rot = Quaternion.Inverse(pivot.localRotation);
        transform.forward = -normal;
        transform.rotation *= rot;
        Vector3 disp = position - pivot.position;
        transform.position += disp;
        rigidbody.isKinematic = true;
    }

    public void rotate_anchor(Vector3 axis, float angle)
    {
        transform.RotateAround(pivot.position, axis, angle);
    }

    public void snap_anchor_rotation()
    {
        Debug.Log("snapping");

        Quaternion pivot_rot = pivot.localRotation;

        float x = pivot.rotation.eulerAngles.x;
        float y = pivot.rotation.eulerAngles.y;
        float z = pivot.rotation.eulerAngles.z;
        x = Mathf.Round(x / 45) * 45 - x;
        y = Mathf.Round(y / 45) * 45 - y;
        z = Mathf.Round(z / 45) * 45 - z;

        transform.RotateAround(pivot.transform.position, pivot.right, x);
        transform.RotateAround(pivot.transform.position, pivot.forward, z);
        transform.RotateAround(pivot.transform.position, pivot.up, y);
    }

    public static item spawn(string name, Vector3 position)
    {
        var i = Resources.Load<item>("items/" + name).inst();
        i.transform.position = position;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        i.rigidbody.velocity = Random.onUnitSphere;
        return i;
    }

    float personal_rand;
    private void Start()
    {
        transform.Rotate(0, Random.Range(0, 360), 0);
        personal_rand = Random.Range(0, 1f);
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F))
        {
            // Suck up loose objects

            // Work out distance to player attraction point
            Vector3 to_player = player.item_attraction_point +
                Vector3.up * personal_rand * 0.15f - transform.position;

            if (to_player.magnitude < COLLECTION_DRAG_DISTANCE)
            {
                // Drag towards the player
                Vector3 v = COLLECTION_DRAG_DISTANCE * to_player.normalized / to_player.magnitude;
                if (v.magnitude > MAX_COLLECTION_SPEED) v *= MAX_COLLECTION_SPEED / v.magnitude;
                rigidbody.velocity = v;

                if (to_player.magnitude < COLLECTION_DISTANCE)
                {
                    // It's close enough, collect the item
                    if (player.inventory.add_item(this.name))
                    {
                        // Create a popup message saying what we picked up, and
                        // how many of them we now have
                        string popup = "+" + char.ToUpper(name[0]) + name.Substring(1);
                        int count = player.inventory.get_count(name);
                        popup += " (" + count + ")";
                        popup_message.create(popup);

                        // Destroy the in-game item representation
                        Destroy(this.gameObject);
                    }
                }
            }
        }
    }
}
