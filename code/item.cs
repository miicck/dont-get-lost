using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : MonoBehaviour
{
    public const float COLLECTION_DISTANCE = 4f;
    public const float MAX_COLLECTION_SPEED = 4f;
    public const float SPIN_SPEED = 30f;
    public const float SPIN_SPEED_RAND = 20f;

    new Rigidbody rigidbody;
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

            if (to_player.magnitude < COLLECTION_DISTANCE)
            {
                // Collect the item
                Vector3 v = COLLECTION_DISTANCE * to_player.normalized / to_player.magnitude;
                if (v.magnitude > MAX_COLLECTION_SPEED)
                    v *= MAX_COLLECTION_SPEED / v.magnitude;
                transform.position += v * Time.deltaTime;

                if (to_player.magnitude < 1f)
                {
                    popup_message.create("+" + char.ToUpper(name[0]) + name.Substring(1));
                    Destroy(this.gameObject);
                }
            }
        }
    }
}
