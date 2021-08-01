using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fishing_float : MonoBehaviour
{
    public float height = 1f;
    public float strike_line = 0.5f;

    public bool strikeable => (transform.position + Vector3.up * strike_line * height / 2).y < world.SEA_LEVEL;
    public float submerged_amount => Mathf.Clamp(1f - (transform.position.y + height * 0.5f - world.SEA_LEVEL) / height, 0f, 1f);
    public float water_depth => world.SEA_LEVEL - world.terrain_altitude(transform.position);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.2f, 1f, 0.2f) * height);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            transform.position + Vector3.up * strike_line * height / 2f,
            new Vector3(0.2f, 0f, 0.2f) * height);
    }
}