using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class version_control
{
    public static string commit_hash { get; private set; }
    public static string commit_date { get; private set; }
    public static string version { get; private set; }

    static string find_git()
    {
        foreach (var to_try in new string[]
        {
            "git",
            "C:\\Program Files\\Git\\cmd\\git.exe"
        })
            if (run(to_try, "--version") != null) return to_try;
        return null;
    }

#if UNITY_EDITOR
    [UnityEditor.Callbacks.DidReloadScripts]
#endif
    public static void on_startup()
    {
#if UNITY_EDITOR

        // Generate server data
        generate_server_data();

        // Get version info from git repo
        string git = find_git();
        if (git == null)
        {
            commit_hash = "Could not find git.";
            commit_date = "Could not find git";
            version = "Could not find git";
            return;
        }

        commit_hash = run(git, "rev-parse HEAD").remove_special_characters();
        string date_command = @"show --no-patch --no-notes --pretty='%cd' " +
                              @"--date=format:'%Y.%m.%d.%H.%M.%S' " + commit_hash;

        string formatted_date = run(git, date_command, env: new Dictionary<string, string> { ["TZ"] = "UTC" });
        formatted_date = formatted_date.remove_special_characters(' ', ':').ToLower();

        var split_date = formatted_date.Split('.');
        commit_date = string.Format("{2}/{1}/{0} {3}:{4}:{5}", split_date);

        version = string.Format("{0}.{1}.{2}.", split_date);
        version += run(git, "rev-parse --short HEAD");

        version = version.remove_special_characters();
        commit_hash = commit_hash.remove_special_characters();
        commit_date = commit_date.remove_special_characters(' ', ':', '/');

        GameObject version_prefab;
        string asset_path;

        var found = Resources.Load("version_info");
        if (found != null)
        {
            // Load version asset from resources
            asset_path = UnityEditor.AssetDatabase.GetAssetPath(found);
            version_prefab = UnityEditor.PrefabUtility.LoadPrefabContents(asset_path);
        }
        else
        {
            // Create version asset
            asset_path = "Assets/resources/version_info.prefab";
            version_prefab = new GameObject("version_info");
            new GameObject("1").transform.SetParent(version_prefab.transform);
            new GameObject("2").transform.SetParent(version_prefab.transform);
            new GameObject("3").transform.SetParent(version_prefab.transform);
        }

        // Save new version asset as prefab (so it's hard-coded(ish) into the built game)
        version_prefab.transform.GetChild(0).name = version;
        version_prefab.transform.GetChild(1).name = commit_hash;
        version_prefab.transform.GetChild(2).name = commit_date;
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(version_prefab, asset_path);

        Debug.Log("Updated version info to " + version);
#else
        // Get version info from prefab
        var go = Resources.Load<GameObject>("version_info");
        if (go == null)
        {
            version = "UNKOWN";
            commit_hash = "UNKNOWN";
            commit_date = "UNKNOWN";
        }
        else
        {
            version = go.transform.GetChild(0).name;
            commit_hash = go.transform.GetChild(1).name;
            commit_date = go.transform.GetChild(2).name;
        }
#endif
    }

    public static string info()
    {
        return "    Version     : " + version + "\n" +
               "    Git commit  : " + commit_hash + "\n" +
               "    Commit date : " + commit_date;
    }

    /// <summary> Run the given program, with the given 
    /// arguments, in the Assets/ directory. </summary>
    static string run(string program, string arguments, Dictionary<string, string> env = null)
    {
        var start_info = new System.Diagnostics.ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Application.dataPath
        };

        if (env != null)
            foreach (var kv in env)
                start_info.EnvironmentVariables[kv.Key] = kv.Value;

        using (var proc = new System.Diagnostics.Process { StartInfo = start_info })
        {
            try
            {
                proc.Start();
            }
            catch
            {
                return null;
            }

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(stderr))
                throw new System.Exception(stderr);

            return stdout;
        }
    }

#if UNITY_EDITOR
    public static void generate_server_data()
    {
        // Loop over all networked prefabs
        string to_write = "";
        foreach (var path in UnityEditor.AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/resources/")) continue;
            string lookup_path = path.Replace(".prefab", "").Replace("Assets/resources/", "");
            var nw = networked.look_up(lookup_path, error_on_fail: false);
            if (nw == null) continue;

            to_write += lookup_path + " " + nw.network_radius() + " " + nw.persistant() + "\n";
        }
        System.IO.File.WriteAllText(Application.dataPath + "/server_data", to_write);
    }
#endif
}
