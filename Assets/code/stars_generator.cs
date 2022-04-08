using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stars_generator : MonoBehaviour
{
    List<Renderer> star_renderers = new List<Renderer>();

    void Start()
    {
        for (int i = 0; i < 1000; ++i)
        {

            float phi = Random.Range(0, 2 * Mathf.PI);
            float theta = Random.Range(0, Mathf.PI);

            Vector3 pos = Random.onUnitSphere * 0.99f;
            if (pos.y < 0) continue;

            var star = Resources.Load<GameObject>("stars/simple_star").inst();

            star.transform.SetParent(transform);
            star.transform.localPosition = pos;
            star.transform.forward = pos;
            star.transform.localScale = Vector3.one * Random.Range(0.4f, 1f) * 0.01f;

            star_renderers.Add(star.GetComponentInChildren<Renderer>());
        }
    }

    private void Update()
    {
        float alpha = 1f - time_manager.time_to_brightness;
        alpha = alpha * alpha;

        var color = new Color(1, 1, 1, alpha);

        foreach (var r in star_renderers)
            utils.set_color(r.material, color);
    }
}
