using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class note
{
    public float pitch;
    public float volume;
    public float fade_in_time;
}

static class scales
{
    public static readonly int[] minor_scale = new int[] { 0, 2, 3, 5, 7, 8, 10 };
    public static readonly int[] major_scale = new int[] { 0, 2, 4, 5, 7, 9, 11 };
    public static readonly int[] blues_scale = new int[] { 0, 3, 5, 7, 10 };

    public static int[] random_scale()
    {
        List<int[]> scales = new List<int[]>
        {
            minor_scale,
            major_scale,
            blues_scale
        };

        return scales[Random.Range(0, scales.Count)];
    }
}

abstract class note_generator
{
    public readonly float semitone = Mathf.Pow(2f, 1f / 12f);
    public float random_pitch(int[] scale) => Mathf.Pow(semitone, scale[Random.Range(0, scale.Length)]);
    public float nth_pitch(int[] scale, int n) => Mathf.Pow(semitone, scale[n % scale.Length]);

    public int[] scale { get; private set; }

    public note_generator(int[] scale)
    {
        this.scale = scale;
    }

    public abstract List<note> generate_notes(int quarter_beat);
    public virtual note_generator mutate() => this;

    protected void normalize_chords(List<note> notes)
    {
        // Normalize volume for chords
        float volume_scale = 1f;
        switch (notes.Count)
        {
            case 1:
                volume_scale *= 1f;
                break;

            default:
                volume_scale *= 0.5f;
                break;
        }

        // Make higher pitch notes quieter
        for (int i = 0; i < notes.Count; ++i)
            notes[i].volume = volume_scale;
    }
}

class random_multiples : note_generator
{
    int[] multiples;
    float[] probabilities;

    public random_multiples(int[] scale) : base(scale)
    {
        multiples = new int[]
        {
            1, // Quarter beat
            2, // Half beat
            4, // Beat
            8, // Half bar
            16, // Bar
        };

        float busyness = Random.Range(0, 1f);

        probabilities = new float[multiples.Length];
        for (int i = 0; i < probabilities.Length; ++i)
            probabilities[i] = Random.Range(0, 0.8f) * busyness;
    }

    public override List<note> generate_notes(int quarter_beat)
    {
        List<note> notes = new List<note>();

        for (int m = 0; m < multiples.Length; ++m)
            if (quarter_beat % multiples[m] == 0)
                if (Random.Range(0, 1f) < probabilities[m])
                    notes.Add(new note
                    {
                        pitch = random_pitch(scale),
                        volume = 1f,
                        fade_in_time = Random.Range(0.05f, 0.2f)
                    });

        normalize_chords(notes);
        return notes;
    }

    public override note_generator mutate()
    {
        return new random_multiples(scale);
    }
}

class walking_bass_line : note_generator
{
    int[] pattern;

    public walking_bass_line(int[] scale) : base(scale)
    {
        randomize_pattern();
    }

    void randomize_pattern()
    {
        pattern = new int[]
        {
            0, 0, 0, 0
        };

        for (int i = 1; i < pattern.Length; ++i)
        {
            // Generate a random pattern, so that two
            // adjacent notes are not at the same position
            int n = pattern[i - 1];
            while (n == pattern[i - 1])
                n = Random.Range(1, scale.Length);
            pattern[i] = n;
        }
    }

    public override List<note> generate_notes(int quarter_beat)
    {
        if (quarter_beat % 16 != 0) // Not at the start of a bar
            return new List<note>();

        int n = pattern[(quarter_beat / 16) % pattern.Length];

        return new List<note>()
        {
            new note()
            {
                pitch = nth_pitch(scale, n) / 4,
                volume = 1f,
                fade_in_time = 1f
            }
        };
    }

    public override note_generator mutate()
    {
        randomize_pattern();
        return this;
    }
}

public class music_generator : MonoBehaviour
{
    public const float BPM = 80;
    static music_generator music_instance;

    public float volume = 1f;

    float time = 0;
    int quarter_beat = 0;
    List<note_generator> note_generators;

    class NotePlayer : MonoBehaviour
    {
        AudioSource source;
        note note;

        private void Update()
        {
            source.volume = Mathf.Min(source.volume + Time.deltaTime / note.fade_in_time, note.volume);

            if (!source.isPlaying)
                Destroy(gameObject);
        }

        public static void play(note note)
        {
            var player = new GameObject("Note").AddComponent<NotePlayer>();
            player.note = note;
            player.source = player.gameObject.AddComponent<AudioSource>();
            player.source.volume = note.fade_in_time > 0 ? 0 : note.volume;
            player.source.pitch = note.pitch;
            player.source.PlayOneShot(Resources.Load<AudioClip>("sounds/music_generator/mandolin_c"));
            player.transform.SetParent(music_instance.transform);
        }
    }

    void Start()
    {
        music_instance = this;
        set_generators(scales.blues_scale);
        volume = options_menu.get_float("music_volume");
    }

    void set_generators(int[] scale)
    {
        note_generators = new List<note_generator>
        {
            new random_multiples(scale),
            new walking_bass_line(scale)
        };
    }

    void on_quarter_beat()
    {
        if (quarter_beat % 16 == 0)
        {
            // Every bar, mutate a random generator
            int to_mutate = Random.Range(0, note_generators.Count);
            note_generators[to_mutate] = note_generators[to_mutate].mutate();
        }

        List<note> notes = new List<note>();
        foreach (var gen in note_generators)
            notes.AddRange(gen.generate_notes(quarter_beat));

        foreach (var n in notes)
            n.volume *= 0.2f * volume;

        foreach (var note in notes)
            NotePlayer.play(note);
    }

    void Update()
    {
        time += Time.deltaTime;
        if ((time * BPM * 4) / 60f > quarter_beat)
        {
            on_quarter_beat();
            ++quarter_beat;
        }
    }
}
