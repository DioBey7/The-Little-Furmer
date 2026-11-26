// TEMPORARY test harness - lives only in the FurmerTest copy, never committed.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayTestRunner
{
    public const string FlagKey = "FurmerPlayTest.Active";

    public static void Run()
    {
        string dir = Application.persistentDataPath;
        Debug.Log("[PLAYTEST] persistentDataPath = " + dir);
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                string n = Path.GetFileName(f);
                if (n.StartsWith("ArcadeIdleSave") || n.StartsWith("webstorage_")) { File.Delete(f); Debug.Log("[PLAYTEST] deleted old save " + n); }
            }
        }

        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(FlagKey, true);
        Debug.Log("[PLAYTEST] entering play mode");
        EditorApplication.EnterPlaymode();
    }
}
