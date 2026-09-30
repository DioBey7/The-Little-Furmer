using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

#if UNITY_EDITOR
    public bool useInspectorData = false;
    public bool resetDataOnStart = false;
#endif

    public GameData Data = new GameData();

    public bool isGameActive = false;

    private string savePath;
    private bool isSavePending = false;

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

        savePath = Path.Combine(Application.persistentDataPath, "ArcadeIdleSave.json");
        LoadData();
        LevelSpawn();
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

    public void SaveData()
    {
        if (!isSavePending)
        {
            isSavePending = true;
            Invoke(nameof(PerformSave), 2f);
        }
    }

    private void PerformSave()
    {
        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(savePath, json);
        isSavePending = false;
    }

    public void ForceImmediateSave()
    {
        CancelInvoke(nameof(PerformSave));
        PerformSave();
    }

    public void LoadData()
    {
#if UNITY_EDITOR
        if (resetDataOnStart)
        {
            Data = new GameData();
            ForceImmediateSave();
            return;
        }

        if (useInspectorData)
        {
            ForceImmediateSave();
            return;
        }
#endif

        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            Data = JsonUtility.FromJson<GameData>(json);
        }
        else
        {
            Data = new GameData();
            ForceImmediateSave();
        }
    }

    public void ResetData()
    {
        Data = new GameData();
        ForceImmediateSave();

        if (UiManager.Instance != null)
        {
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
        Data.currentLevelIndex++;
        Data.currentObjectiveProgress.Clear();
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
}
