using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class material_sound : MonoBehaviour
{
    public enum TYPE
    {
        HIT,
        STEP,
    }

    public AudioClip hit_sound;
    public float hit_volume = 1f;

    public AudioClip step_sound;
    public float step_volume = 1f;

    static material_sound load(Material material)
    {
        string name = "default";

        if (material != null)
        {
            name = material.name;
            name = name.Replace("(Instance)", "");
            name = name.Trim();
        }

        var mat_sound = Resources.Load<material_sound>("sounds/materials/" + name);
        if (mat_sound == null)
            mat_sound = Resources.Load<material_sound>("sounds/materials/default");

        return mat_sound;
    }

    public static AudioClip sound(TYPE type, Material material, out float volume)
    {
        var mat = load(material);
        switch (type)
        {
            case TYPE.HIT:
                volume = mat.hit_volume;
                return mat.hit_sound;

            case TYPE.STEP:
                volume = mat.step_volume;
                return mat.step_sound;

            default:
                throw new System.Exception("Unkown sound type!");
        }
    }

    public static void play(TYPE type, Vector3 position, Material material)
    {
        var mat_sound = load(material);
        var player = new GameObject("material sound: " + mat_sound.name).AddComponent<AudioSource>();
        switch (type)
        {
            case TYPE.HIT:
                player.clip = mat_sound.hit_sound;
                player.volume = mat_sound.hit_volume;
                break;
        }

        player.spatialBlend = 1f; // Full 3d sound

        // Play immediately
        player.transform.position = position;
        player.playOnAwake = false;
        player.Play();

        // Destroy once played
        Destroy(player.gameObject, player.clip.length * 1.5f);
    }
}
