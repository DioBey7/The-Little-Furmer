using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnlockableAreaConfig
{
    public string areaName = "New Area";
    public int uniqueAreaID = 1;
    public int requiredCashToUnlock = 500;
    public bool isUnlockedByStart = false;
}

[System.Serializable]
public class FieldSettings
{
    [Header("Growth Settings")]
    public float cropGrowthTimeInSeconds = 3f;

    [Header("Field Capacity Upgrades")]
    public int baseUpgradeCost = 500;
    public float upgradeCostMultiplier = 1.5f;
    public float interactionTimeToUpgrade = 1.5f;
}

[System.Serializable]
public class FactorySettings
{
    [System.Serializable]
    public struct FactoryRecipeConfig
    {
        public ItemType factoryOutputType;
        public List<FactoryIngredient> requiredIngredients;
    }

    [System.Serializable]
    public struct FactoryIngredient
    {
        public ItemType itemType;
        public int requiredAmount;
    }

    [Header("Production Settings")]
    public float productionTimePerItem = 3f;
    public float overallProductionSpeedMultiplier = 1f;
    public float itemDepositAnimationTime = 0.2f;

    [Header("Factory Recipes")]
    public List<FactoryRecipeConfig> factoryRecipes = new List<FactoryRecipeConfig>();
}

[System.Serializable]
public class StockAndMarketSettings
{
    [Header("Stock Capacity Upgrades")]
    public int baseUpgradeCost = 1000;
    public float upgradeCostMultiplier = 1.5f;
    public int maximumUpgradeLevel = 5;
    public int startingDisplayCapacity = 4;
    public int capacityIncreasePerLevel = 1;
    public float interactionTimeToUpgrade = 1.0f;

    [Header("Stock Interactions")]
    public float itemTransferDelayToStock = 0.5f;
    public float minDisplayTimeForUI = 1.2f;

    [Header("Market & Customers")]
    public float itemTransferDelayToCustomer = 0.5f;
    public float delayBetweenCustomerSpawns = 10f;
    public int maxActiveCustomers = 5;
}

[System.Serializable]
public class PlayerAndAiSettings
{
    [Header("Player Backpack Upgrades")]
    public int backpackBaseUpgradeCost = 1000;
    public float backpackCostMultiplier = 1.5f;
    public int backpackMaxLevel = 10;
    public int startingBackpackCapacity = 5;
    public int capacityIncreasePerBackpackLevel = 3;
    public float backpackInteractionTimeToUpgrade = 1.5f;

    [Header("AI Worker Capacity Upgrades")]
    public int aiBaseUpgradeCost = 2000;
    public float aiCostMultiplier = 1.5f;
    public int aiMaxLevel = 10;
    public int startingAiCapacity = 3;
    public int capacityIncreasePerAiLevel = 1;
    public float aiInteractionTimeToUpgrade = 1.5f;

    [Header("AI Worker Hiring (Buy Area)")]
    public int costToHireNewWorker = 1000;
    public float interactionTimeToHireWorker = 1.5f;
    public int maximumWorkersAllowed = 5;
}

[System.Serializable]
public class AreaUnlockMechanics
{
    public float circleFillTime = 1.0f;
    public float cashDeductionInterval = 0.05f;
    public int cashDeductedPerInterval = 25;
}

[System.Serializable]
public class LevelConfig
{
    [Header("Core References")]
    public GameObject levelPrefab;

    [Header("Dynamic Objective Targets")]
    public int baseMoneyObjectiveTarget = 100;
    public int baseHarvestObjectiveTarget = 2;
    public int baseProduceObjectiveTarget = 1;

    public List<ItemType> availableItemTypes = new List<ItemType>();
    public int minDifferentItemTypes = 1;
    public int maxDifferentItemTypes = 2;
    public int minAmountPerItem = 1;
    public int maxAmountPerItem = 3;

    [Header("Manual Objectives Override")]
    public List<Level.LevelObjective> inspectorObjectives = new List<Level.LevelObjective>();

    [Space(15)]
    [Header("AREAS (FIELDS, FACTORIES, STOCKS, GENERAL)")]
    [Space(5)]

    [Header("1. FIELD AREAS")]
    public FieldSettings fieldSettings = new FieldSettings();
    public List<UnlockableAreaConfig> fieldAreasToUnlock = new List<UnlockableAreaConfig>();

    [Header("2. FACTORY AREAS")]
    public FactorySettings factorySettings = new FactorySettings();
    public List<UnlockableAreaConfig> factoryAreasToUnlock = new List<UnlockableAreaConfig>();

    [Header("3. STOCK & MARKET AREAS")]
    public StockAndMarketSettings stockAndMarketSettings = new StockAndMarketSettings();
    public List<UnlockableAreaConfig> stockAreasToUnlock = new List<UnlockableAreaConfig>();

    [Header("4. GENERAL (Player, AI, Unlocking)")]
    public PlayerAndAiSettings playerAndAiSettings = new PlayerAndAiSettings();
    public AreaUnlockMechanics unlockMechanics = new AreaUnlockMechanics();
    public List<UnlockableAreaConfig> generalAreasToUnlock = new List<UnlockableAreaConfig>();
}

[CreateAssetMenu(fileName = "MasterLevelData", menuName = "ArcadeIdle/MasterLevelData")]
public class LevelDataSO : ScriptableObject
{
    public List<LevelConfig> levels = new List<LevelConfig>();
}

public interface IInteractableArea
{
    void OnPlayerEnter(ItemCollecting player);
    void OnPlayerExit(ItemCollecting player);
    void ProcessInteraction(float deltaTime, ItemCollecting player);
}
