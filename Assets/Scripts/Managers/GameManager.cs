using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

#if UNITY_EDITOR
    public bool useInspectorData = false;
    public bool resetDataOnStart = false;
#endif

    public GameData Data = GameData.CreateNew();

    public bool isGameActive = false;

    // Saves requested during play are batched and written this many real-time seconds later.
    // Real time is used so saves still happen while the pause menu has Time.timeScale at 0.
    private const float SaveDelaySeconds = 1f;

    private const string SaveFileName = "ArcadeIdleSave.json";
    private const string SaveKey = "save";
    private const string BackupSaveKey = "save_bak";

    private string savePath;
    private string tempSavePath;
    private string backupSavePath;

    private bool isSavePending = false;
    private float saveDueTime = 0f;
    private bool isDataLoaded = false;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        tempSavePath = savePath + ".tmp";
        backupSavePath = savePath + ".bak";

        LoadData();
        LevelSpawn();
    }

    private void Update()
    {
        if (isSavePending && Time.unscaledTime >= saveDueTime)
        {
            PerformSave();
        }
    }

    public void SetGameState(bool state)
    {
        isGameActive = state;
    }

    private void LevelSpawn()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadLevel(Data.currentLevelIndex, Data.currentObjectiveProgress);
        }
    }

    /// <summary>Requests a save. Several requests close together are written once.</summary>
    public void SaveData()
    {
        if (!isSavePending)
        {
            isSavePending = true;
            saveDueTime = Time.unscaledTime + SaveDelaySeconds;
        }
    }

    /// <summary>Writes the save right away. Safe to call at any time, for example when the browser tab is hidden.</summary>
    public void ForceImmediateSave()
    {
        PerformSave();
    }

    private void PerformSave()
    {
        isSavePending = false;
        if (!isDataLoaded || Data == null) return;

        try
        {
            Data.saveVersion = Mathf.Max(Data.saveVersion, GameData.CurrentSaveVersion);
            string json = JsonUtility.ToJson(Data, true);
            WriteSave(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] Failed to write save: {e}");
        }
    }

    public void LoadData()
    {
#if UNITY_EDITOR
        if (resetDataOnStart)
        {
            Data = GameData.CreateNew();
            isDataLoaded = true;
            ForceImmediateSave();
            return;
        }

        if (useInspectorData)
        {
            if (Data == null) Data = GameData.CreateNew();
            PrepareLoadedData(Data);
            isDataLoaded = true;
            ForceImmediateSave();
            return;
        }
#endif

        GameData loaded = null;

        string primary = ReadSave(false);
        if (primary != null)
        {
            loaded = TryParse(primary, "save");
        }

        if (loaded == null)
        {
            string backup = ReadSave(true);
            if (backup != null)
            {
                loaded = TryParse(backup, "backup save");
                if (loaded != null) Debug.LogWarning("[GameManager] Main save was missing or damaged. Restored the backup save.");
            }
        }

        bool isNewGame = loaded == null;
        if (isNewGame)
        {
            if (primary != null) Debug.LogWarning("[GameManager] Save could not be read. Starting a new game.");
            loaded = GameData.CreateNew();
        }

        PrepareLoadedData(loaded);
        Data = loaded;
        isDataLoaded = true;

        if (isNewGame) ForceImmediateSave();
    }

    private static GameData TryParse(string json, string label)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonUtility.FromJson<GameData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] Could not parse {label}: {e.Message}");
            return null;
        }
    }

    private static void PrepareLoadedData(GameData data)
    {
        data.Sanitize();
        MigrateSave(data);

        // Level-scoped data that belongs to another level would unlock or upgrade the wrong objects,
        // because area, stock and field IDs repeat between levels.
        if (data.levelProgressIndex != data.currentLevelIndex)
        {
            data.ClearLevelScopedProgress();
        }
    }

    private static void MigrateSave(GameData data)
    {
        if (data.saveVersion > GameData.CurrentSaveVersion)
        {
            Debug.LogWarning($"[GameManager] Save version {data.saveVersion} is newer than this build ({GameData.CurrentSaveVersion}). Loading it as is.");
            return;
        }

        if (data.saveVersion < 1)
        {
            // Version 0 kept unlocked areas and upgrades from every level in one list, so past level 1
            // they can't be told apart. Drop them; earlier levels' progress was mixed in.
            if (data.currentLevelIndex > 0)
            {
                List<int> keepObjectives = new List<int>(data.currentObjectiveProgress);
                data.ClearLevelScopedProgress();
                data.currentObjectiveProgress.AddRange(keepObjectives);
            }
            data.levelProgressIndex = data.currentLevelIndex;
            data.saveVersion = 1;
        }

        // Add future migrations here: if (data.saveVersion < 2) { ...; data.saveVersion = 2; }
    }

    public void ResetData()
    {
        Data = GameData.CreateNew();
        isDataLoaded = true;
        ForceImmediateSave();

        if (UiManager.Instance != null)
        {
            if (UiManager.Instance.CurrentCash != 0) UiManager.Instance.SpendCash(UiManager.Instance.CurrentCash);
            UiManager.Instance.UpdateCashUI();
        }
    }

    public void UpdateObjectiveProgress(List<int> progress)
    {
        Data.currentObjectiveProgress.Clear();
        Data.currentObjectiveProgress.AddRange(progress);
    }

    public void CompleteLevel()
    {
        // Each level starts with no cash and its own areas, upgrades and workers.
        if (UiManager.Instance != null && UiManager.Instance.CurrentCash != 0)
        {
            UiManager.Instance.SpendCash(UiManager.Instance.CurrentCash);
        }
        Data.currentCash = 0;

        Data.currentLevelIndex++;
        Data.ClearLevelScopedProgress();
        ForceImmediateSave();

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadLevel(Data.currentLevelIndex, Data.currentObjectiveProgress);
        }
    }

    private void OnApplicationQuit()
    {
        ForceImmediateSave();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ForceImmediateSave();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isSavePending)
        {
            ForceImmediateSave();
        }
    }

    // ---------- Storage ----------
    // WebGL: browser localStorage through WebStorage (survives itch.io re-uploads, no IndexedDB flush needed).
    // Other platforms: a file in persistentDataPath, written to a temp file first and swapped in,
    // with the previous good save kept as a backup.

    private string ReadSave(bool backup)
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string key = backup ? BackupSaveKey : SaveKey;
            return WebStorage.HasKey(key) ? WebStorage.Load(key) : null;
#else
            string path = backup ? backupSavePath : savePath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] Could not read {(backup ? "backup save" : "save")}: {e.Message}");
            return null;
        }
    }

    private void WriteSave(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string previous = WebStorage.HasKey(SaveKey) ? WebStorage.Load(SaveKey) : null;
        if (!string.IsNullOrEmpty(previous) && TryParse(previous, "previous save") != null)
        {
            WebStorage.Save(BackupSaveKey, previous);
        }
        WebStorage.Save(SaveKey, json);
#else
        File.WriteAllText(tempSavePath, json);

        if (File.Exists(savePath))
        {
            bool currentIsValid = TryParse(File.ReadAllText(savePath), "previous save") != null;
            try
            {
                if (currentIsValid)
                {
                    File.Replace(tempSavePath, savePath, backupSavePath);
                    return;
                }
            }
            catch (Exception)
            {
                // File.Replace isn't supported on every file system. Fall through to copy and move.
            }

            if (currentIsValid) File.Copy(savePath, backupSavePath, true);
            File.Delete(savePath);
        }

        File.Move(tempSavePath, savePath);
#endif
    }
}
