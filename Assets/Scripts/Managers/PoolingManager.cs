using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{
    public static PoolingManager Instance { get; private set; }

    private Dictionary<ItemType, List<GameObject>> pools = new Dictionary<ItemType, List<GameObject>>();
    private Dictionary<ItemType, Transform> parentFolders = new Dictionary<ItemType, Transform>();
    private Dictionary<ItemType, int> fieldCapacities = new Dictionary<ItemType, int>();
    private Dictionary<ItemType, GameObject> prefabCache = new Dictionary<ItemType, GameObject>();
    private Dictionary<ItemType, Vector3> scaleCache = new Dictionary<ItemType, Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        System.Array itemTypes = System.Enum.GetValues(typeof(ItemType));
        foreach (ItemType type in itemTypes)
        {
            GameObject folder = new GameObject(type.ToString() + "_Pool");
            folder.transform.SetParent(transform);
            parentFolders[type] = folder.transform;
            pools[type] = new List<GameObject>(50);
            fieldCapacities[type] = 0;
        }
    }

    private void Start()
    {
        if (ItemManager.Instance == null) return;

        System.Array itemTypes = System.Enum.GetValues(typeof(ItemType));
        foreach (ItemType type in itemTypes)
        {
            ItemData data = ItemManager.Instance.GetItemByType(type);
            if (data != null && data.itemPrefab != null)
            {
                prefabCache[type] = data.itemPrefab;
                scaleCache[type] = data.itemPrefab.transform.localScale;

                Transform folder = parentFolders[type];
                for (int i = 0; i < 18; i++)
                {
                    GameObject item = Instantiate(data.itemPrefab, folder);
                    item.SetActive(false);
                    pools[type].Add(item);
                }
            }
        }
    }

    public void PrewarmPoolForType(ItemType type, int count)
    {
        if (!pools.ContainsKey(type)) return;

        fieldCapacities[type] += count;

        if (!prefabCache.TryGetValue(type, out GameObject prefab) || prefab == null)
        {
            ItemData data = ItemManager.Instance != null ? ItemManager.Instance.GetItemByType(type) : null;
            if (data == null || data.itemPrefab == null) return;
            prefab = data.itemPrefab;
            prefabCache[type] = prefab;
            scaleCache[type] = prefab.transform.localScale;
        }

        Transform folder = parentFolders[type];
        for (int i = 0; i < count; i++)
        {
            GameObject item = Instantiate(prefab, folder);
            item.SetActive(false);
            pools[type].Add(item);
        }
    }

    public GameObject GetItem(ItemType type)
    {
        List<GameObject> pool = pools[type];

        if (!prefabCache.TryGetValue(type, out GameObject prefab) || prefab == null)
        {
            ItemData data = ItemManager.Instance != null ? ItemManager.Instance.GetItemByType(type) : null;
            if (data == null || data.itemPrefab == null) return null;
            prefab = data.itemPrefab;
            prefabCache[type] = prefab;
            scaleCache[type] = prefab.transform.localScale;
        }

        GameObject item;
        Transform folder = parentFolders[type];

        if (pool.Count > 0)
        {
            int lastIndex = pool.Count - 1;
            item = pool[lastIndex];
            pool.RemoveAt(lastIndex);

            if (item.transform.parent != folder) item.transform.SetParent(folder);
            item.SetActive(true);
        }
        else
        {
            item = Instantiate(prefab, folder);
        }

        item.transform.localScale = scaleCache[type];

        int threshold = Mathf.Max(10, fieldCapacities[type]);
        if (pool.Count < threshold)
        {
            for (int i = 0; i < 18; i++)
            {
                GameObject itemTemp = Instantiate(prefab, folder);
                itemTemp.SetActive(false);
                pool.Add(itemTemp);
            }
        }

        return item;
    }

    public void ReturnItem(ItemType type, GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        Transform folder = parentFolders[type];
        if (obj.transform.parent != folder)
        {
            obj.transform.SetParent(folder);
        }
        pools[type].Add(obj);
    }

    public Transform GetFolder(ItemType type)
    {
        return parentFolders.ContainsKey(type) ? parentFolders[type] : transform;
    }
}
