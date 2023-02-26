using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class version_info : MonoBehaviour
{
    public string version;
    public string commit_date;
    public string commit_hash;
    public string contributors;

    public string formatted()
    {
        return "    Version     : " + version + "\n" +
               "    Git commit  : " + commit_hash + "\n" +
               "    Commit date : " + commit_date;
    }
}