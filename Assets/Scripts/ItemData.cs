using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Tomato,
    Carrot,
    Onion,
    Potato,
    Garlic
}

[System.Serializable]
public class ItemData
{
    public ItemType itemType = ItemType.Tomato;
    public GameObject itemPrefab;
    public int itemValue = 100;
    public float stackHeight = 0.2f;

    public int CashSpawnCount
    {
        get
        {
            int defaultCashValue = CashManager.Instance != null ? CashManager.Instance.defaultCashValue : 100;
            if (defaultCashValue <= 0) defaultCashValue = 100;

            return itemValue / defaultCashValue;
        }
    }
}
