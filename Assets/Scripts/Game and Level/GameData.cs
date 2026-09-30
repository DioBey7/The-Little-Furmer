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
    public int currentLevelIndex = 0;
    public List<int> currentObjectiveProgress = new List<int>();

    public int currentCash = 0;
    public List<int> unlockedAreas = new List<int>();

    public int backpackUpgradeLevel = 0;
    public int aiCapacityUpgradeLevel = 0;

    public List<StockSaveData> stockUpgrades = new List<StockSaveData>();
    public List<AreaProgressData> areaProgress = new List<AreaProgressData>();
    public List<AiWorkerSaveData> purchasedAiWorkers = new List<AiWorkerSaveData>();
    public List<StockSaveData> fieldUpgrades = new List<StockSaveData>();
}
