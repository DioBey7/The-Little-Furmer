using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    public List<ItemData> itemDatabase = new List<ItemData>();

    private Dictionary<ItemType, ItemData> itemDictionary = new Dictionary<ItemType, ItemData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < itemDatabase.Count; i++)
        {
            if (itemDatabase[i] != null)
            {
                itemDictionary[itemDatabase[i].itemType] = itemDatabase[i];
            }
        }
    }

    public ItemData GetItemByType(ItemType type)
    {
        if (itemDictionary.TryGetValue(type, out ItemData data))
        {
            return data;
        }
        return null;
    }
}
