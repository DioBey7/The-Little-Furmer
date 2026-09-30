using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    public Transform customerSpawnPoint;
    public Transform aiCharacterSpawnPoint;
    public MarketArea marketArea;
    public Transform customerExitPoint;

    public List<FactoryArea> factoryAreas = new List<FactoryArea>();
    public List<Field> fields = new List<Field>();
    public List<StockArea> stockAreas = new List<StockArea>();

    public enum ObjectiveType { HarvestItem, ProduceItem, SellItem, ServeCustomer, EarnMoney }

    [System.Serializable]
    public class LevelObjective
    {
        public ObjectiveType type;
        public ItemType itemType;
        public int targetAmount;

        [HideInInspector] public int currentAmount;

        public bool IsCompleted => currentAmount >= targetAmount;
    }

    [HideInInspector]
    public List<LevelObjective> objectives = new List<LevelObjective>();

    public void GenerateDynamicObjectives(int levelIndex, LevelConfig data)
    {
        objectives.Clear();

        LevelObjective moneyObj = new LevelObjective();
        moneyObj.type = ObjectiveType.EarnMoney;
        moneyObj.targetAmount = data.baseMoneyObjectiveTarget + (levelIndex * 100);
        objectives.Add(moneyObj);

        ItemType randomBasicItem = ItemType.Tomato;
        if (data.availableItemTypes != null && data.availableItemTypes.Count > 0)
        {
            randomBasicItem = data.availableItemTypes[Random.Range(0, Mathf.Min(2, data.availableItemTypes.Count))];
        }

        LevelObjective harvestObj = new LevelObjective();
        harvestObj.type = ObjectiveType.HarvestItem;
        harvestObj.itemType = randomBasicItem;
        harvestObj.targetAmount = data.baseHarvestObjectiveTarget + (levelIndex * 2);
        objectives.Add(harvestObj);
    }
}
