using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class version_control
{
    public static string commit_hash;
    public static string commit_date;
    public static string version;

    public static void on_startup()
    {
#if UNITY_EDITOR
        // Save version info to prefab
        commit_hash = run("git", "rev-parse HEAD").remove_special_characters();
        string date_command = @"show --no-patch --no-notes --pretty='%cd' " +
                              @"--date=format:'%Y.%m.%d.%H.%M.%S' " + commit_hash;

        string formatted_date = run("git", date_command, env: new Dictionary<string, string> { ["TZ"] = "UTC" });
        formatted_date = formatted_date.remove_special_characters(' ', ':').ToLower();

        var split_date = formatted_date.Split('.');
        commit_date = string.Format("{2}/{1}/{0} {3}:{4}:{5}", split_date);

        version = string.Format("{0}.{1}.{2}.", split_date);
        version += run("git", "rev-parse --short HEAD");

        version = version.remove_special_characters();
        commit_hash = commit_hash.remove_special_characters();
        commit_date = commit_date.remove_special_characters(' ', ':', '/');

        string asset_path = UnityEditor.AssetDatabase.GetAssetPath(Resources.Load("version_info"));
        var version_prefab = UnityEditor.PrefabUtility.LoadPrefabContents(asset_path);
        version_prefab.transform.GetChild(0).name = version;
        version_prefab.transform.GetChild(1).name = commit_hash;
        version_prefab.transform.GetChild(2).name = commit_date;
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(version_prefab, asset_path);
#else
        // Get version info from prefab
        var go = Resources.Load<GameObject>("version_info");
        version = go.transform.GetChild(0).name;
        commit_hash = go.transform.GetChild(1).name;
        commit_date = go.transform.GetChild(2).name;
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
            proc.Start();

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(stderr))
                throw new System.Exception(stderr);
            return stdout;
        }
    }
}
