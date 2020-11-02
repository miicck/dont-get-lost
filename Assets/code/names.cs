using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class names
{
    static string[] male_names;
    static string[] female_names;

    public static string random_male_name() { return male_names[Random.Range(0, male_names.Length)]; }
    public static string random_female_name() { return female_names[Random.Range(0, female_names.Length)]; }

    static string[] load_names(string resources_path)
    {
        List<string> names = new List<string>();

        var files = Resources.LoadAll<TextAsset>(resources_path);
        foreach (var f in files)
            foreach (var line in f.text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                names.Add(trimmed.ToLower());
            }

        return names.ToArray();
    }

    public static void initialize()
    {
        male_names = load_names("names/male");
        female_names = load_names("names/female");
    }
}
