using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Small key/value store that survives itch.io re-uploads.
/// WebGL player: browser localStorage (see Assets/Plugins/WebGL/WebStorage.jslib).
/// Editor and other platforms: one file per key in Application.persistentDataPath.
/// Load returns null when the key does not exist.
/// </summary>
public static class WebStorage
{
    // localStorage on itch.io is shared by every game served from the same origin,
    // so all keys get a game-specific prefix.
    private const string KeyPrefix = "TheLittleFurmer/";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int WebStorage_Save(string key, string value);
    [DllImport("__Internal")] private static extern string WebStorage_Load(string key);
    [DllImport("__Internal")] private static extern int WebStorage_HasKey(string key);
    [DllImport("__Internal")] private static extern void WebStorage_Delete(string key);

    public static bool HasKey(string key) => WebStorage_HasKey(KeyPrefix + key) == 1;

    public static string Load(string key) => WebStorage_Load(KeyPrefix + key);

    public static void Save(string key, string value)
    {
        if (WebStorage_Save(KeyPrefix + key, value ?? string.Empty) == 0)
            Debug.LogWarning($"[WebStorage] Could not save '{key}' (storage full or blocked by the browser).");
    }

    public static void Delete(string key) => WebStorage_Delete(KeyPrefix + key);
#else
    private static string PathFor(string key)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
        return Path.Combine(Application.persistentDataPath, "webstorage_" + key + ".txt");
    }

    public static bool HasKey(string key) => File.Exists(PathFor(key));

    public static string Load(string key)
    {
        string path = PathFor(key);
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException e) { Debug.LogWarning($"[WebStorage] Could not read '{key}': {e.Message}"); return null; }
    }

    public static void Save(string key, string value)
    {
        try { File.WriteAllText(PathFor(key), value ?? string.Empty); }
        catch (IOException e) { Debug.LogWarning($"[WebStorage] Could not save '{key}': {e.Message}"); }
    }

    public static void Delete(string key)
    {
        string path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
    }
#endif
}
