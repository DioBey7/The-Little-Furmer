using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    public event Action OnObjectiveUpdated;
    public event Action OnAllObjectivesCompleted;

    [SerializeField]
    public List<Level.LevelObjective> currentObjectives = new List<Level.LevelObjective>();

    private bool isLevelCompleted = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeObjectives(List<Level.LevelObjective> levelObjectives, List<int> savedProgress)
    {
        currentObjectives = levelObjectives;
        isLevelCompleted = false;

        if (savedProgress != null && savedProgress.Count == currentObjectives.Count)
        {
            for (int i = 0; i < currentObjectives.Count; i++)
            {
                currentObjectives[i].currentAmount = savedProgress[i];
            }
        }
        else
        {
            for (int i = 0; i < currentObjectives.Count; i++)
            {
                currentObjectives[i].currentAmount = 0;
            }
        }

        OnObjectiveUpdated?.Invoke();

        // The save says this level was already finished (the player closed the game on the
        // Level Completed popup without pressing Continue). Go straight to the next level
        // instead of showing the popup again on startup.
        if (AreAllCompleted())
        {
            isLevelCompleted = true;
            StartCoroutine(AdvanceToNextLevelRoutine());
            return;
        }

        CheckCompletion();
    }

    private bool AreAllCompleted()
    {
        if (currentObjectives == null || currentObjectives.Count == 0) return false;
        for (int i = 0; i < currentObjectives.Count; i++)
        {
            if (!currentObjectives[i].IsCompleted) return false;
        }
        return true;
    }

    private System.Collections.IEnumerator AdvanceToNextLevelRoutine()
    {
        // Let the current LoadLevelRoutine finish before loading the next level.
        yield return null;
        if (LevelManager.Instance != null) LevelManager.Instance.LoadNextLevelFromUI();
    }

    public void ProgressObjective(Level.ObjectiveType type, int amount, ItemType? itemType = null)
    {
        if (currentObjectives == null || currentObjectives.Count == 0 || isLevelCompleted) return;

        bool isUpdated = false;

        for (int i = 0; i < currentObjectives.Count; i++)
        {
            Level.LevelObjective obj = currentObjectives[i];

            if (obj.IsCompleted) continue;

            if (obj.type == type)
            {
                bool typeMatches = true;

                if (itemType.HasValue && (type == Level.ObjectiveType.HarvestItem || type == Level.ObjectiveType.ProduceItem || type == Level.ObjectiveType.SellItem))
                {
                    typeMatches = (obj.itemType == itemType.Value);
                }

                if (typeMatches)
                {
                    obj.currentAmount = Mathf.Min(obj.currentAmount + amount, obj.targetAmount);
                    isUpdated = true;
                }
            }
        }

        if (isUpdated)
        {
            SaveProgress();
            OnObjectiveUpdated?.Invoke();
            CheckCompletion();
        }
    }

    public bool IsObjectiveCompleted(Level.ObjectiveType type, ItemType? itemType = null)
    {
        if (currentObjectives == null) return false;

        for (int i = 0; i < currentObjectives.Count; i++)
        {
            Level.LevelObjective obj = currentObjectives[i];
            if (obj.type == type)
            {
                if (itemType.HasValue && obj.itemType != itemType.Value) continue;
                return obj.IsCompleted;
            }
        }
        return false;
    }

    private void CheckCompletion()
    {
        if (isLevelCompleted || currentObjectives.Count == 0) return;

        bool allCompleted = true;
        for (int i = 0; i < currentObjectives.Count; i++)
        {
            if (!currentObjectives[i].IsCompleted)
            {
                allCompleted = false;
                break;
            }
        }

        if (allCompleted)
        {
            isLevelCompleted = true;
            OnAllObjectivesCompleted?.Invoke();

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.HandleAllObjectivesCompleted();
            }
        }
    }

    private void SaveProgress()
    {
        if (GameManager.Instance != null)
        {
            List<int> progressList = new List<int>();
            for (int i = 0; i < currentObjectives.Count; i++)
            {
                progressList.Add(currentObjectives[i].currentAmount);
            }

            GameManager.Instance.UpdateObjectiveProgress(progressList);
            GameManager.Instance.SaveData();
        }
    }

    [ContextMenu("Debug: Complete All Objectives")]
    public void DebugCompleteAll()
    {
        if (currentObjectives == null || isLevelCompleted) return;

        for (int i = 0; i < currentObjectives.Count; i++)
        {
            currentObjectives[i].currentAmount = currentObjectives[i].targetAmount;
        }

        SaveProgress();
        OnObjectiveUpdated?.Invoke();
        CheckCompletion();
    }
}
