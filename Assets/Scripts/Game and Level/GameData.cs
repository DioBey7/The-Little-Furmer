using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AreaProgressData
{
    public int areaID;
    public int remainingCost;
}

[System.Serializable]
public class StockSaveData
{
    public int stockID;
    public int level;
}

[System.Serializable]
public class AiWorkerSaveData
{
    public ItemType itemType;
    public int count;
}

[System.Serializable]
public class GameData
{
    // Bump when the save layout changes, and add a step to GameManager.MigrateSave.
    // Version 0 = saves written before versioning existed.
    public const int CurrentSaveVersion = 1;

    public int saveVersion = 0;

    public int currentLevelIndex = 0;

    // The level that the level-scoped lists below belong to. Area, stock and field IDs
    // are only unique inside one level, so this data must never carry over to another level.
    public int levelProgressIndex = 0;

    public List<int> currentObjectiveProgress = new List<int>();

    // Level-scoped: reset when a level is completed.
    public int currentCash = 0;
    public List<int> unlockedAreas = new List<int>();

    // Player-wide: kept across levels.
    public int backpackUpgradeLevel = 0;
    public int aiCapacityUpgradeLevel = 0;

    // Level-scoped: reset when a level is completed.

    public List<StockSaveData> stockUpgrades = new List<StockSaveData>();
    public List<AreaProgressData> areaProgress = new List<AreaProgressData>();
    public List<AiWorkerSaveData> purchasedAiWorkers = new List<AiWorkerSaveData>();
    public List<StockSaveData> fieldUpgrades = new List<StockSaveData>();

    public static GameData CreateNew()
    {
        return new GameData { saveVersion = CurrentSaveVersion };
    }

    /// <summary>Clears progress that belongs to a single level (areas, upgrades of level objects, hired workers).</summary>
    public void ClearLevelScopedProgress()
    {
        currentObjectiveProgress.Clear();
        unlockedAreas.Clear();
        areaProgress.Clear();
        stockUpgrades.Clear();
        fieldUpgrades.Clear();
        purchasedAiWorkers.Clear();
        levelProgressIndex = currentLevelIndex;
    }

    /// <summary>Repairs missing or out-of-range values so a damaged or hand-edited save can't break the game.</summary>
    public void Sanitize()
    {
        if (currentObjectiveProgress == null) currentObjectiveProgress = new List<int>();
        if (unlockedAreas == null) unlockedAreas = new List<int>();
        if (stockUpgrades == null) stockUpgrades = new List<StockSaveData>();
        if (areaProgress == null) areaProgress = new List<AreaProgressData>();
        if (purchasedAiWorkers == null) purchasedAiWorkers = new List<AiWorkerSaveData>();
        if (fieldUpgrades == null) fieldUpgrades = new List<StockSaveData>();

        stockUpgrades.RemoveAll(x => x == null);
        areaProgress.RemoveAll(x => x == null);
        purchasedAiWorkers.RemoveAll(x => x == null);
        fieldUpgrades.RemoveAll(x => x == null);

        if (currentLevelIndex < 0) currentLevelIndex = 0;
        if (levelProgressIndex < 0) levelProgressIndex = 0;
        if (currentCash < 0) currentCash = 0;
        if (backpackUpgradeLevel < 0) backpackUpgradeLevel = 0;
        if (aiCapacityUpgradeLevel < 0) aiCapacityUpgradeLevel = 0;
        for (int i = 0; i < currentObjectiveProgress.Count; i++)
        {
            if (currentObjectiveProgress[i] < 0) currentObjectiveProgress[i] = 0;
        }
        // An area with nothing left to pay but not marked unlocked could never be finished.
        areaProgress.RemoveAll(x => x.remainingCost <= 0);
    }
}
