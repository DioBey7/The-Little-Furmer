using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance { get; private set; }

    public List<GameObject> customerPrefabs = new List<GameObject>();
    public MarketArea marketArea;

    public Transform spawnPoint;
    public Transform exitPoint;

    public List<CustomerAI> activeCustomers = new List<CustomerAI>();

    [HideInInspector] public int maxActiveCustomers = 10;
    [HideInInspector] public float spawnInterval = 15f;
    [HideInInspector] public List<ItemType> availableItemTypes = new List<ItemType>();
    [HideInInspector] public int minDifferentItemTypes = 1;
    [HideInInspector] public int maxDifferentItemTypes = 1;
    [HideInInspector] public int minAmountPerItem = 1;
    [HideInInspector] public int maxAmountPerItem = 3;

    private Coroutine spawnCoroutine;
    private List<ItemType> cachedItemPool = new List<ItemType>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetLevelParameters(LevelConfig data)
    {
        activeCustomers.RemoveAll(c => c == null);
        activeCustomers.Clear();

        spawnInterval = data.stockAndMarketSettings.delayBetweenCustomerSpawns;
        maxActiveCustomers = data.stockAndMarketSettings.maxActiveCustomers;

        availableItemTypes = new List<ItemType>(data.availableItemTypes);
        minDifferentItemTypes = data.minDifferentItemTypes;
        maxDifferentItemTypes = data.maxDifferentItemTypes;
        minAmountPerItem = data.minAmountPerItem;
        maxAmountPerItem = data.maxAmountPerItem;

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(CustomerSpawnRoutine());
    }

    private IEnumerator CustomerSpawnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(spawnInterval);

        while (true)
        {
            activeCustomers.RemoveAll(c => c == null);

            if (activeCustomers.Count < maxActiveCustomers)
            {
                SpawnCustomer();
            }
            yield return wait;
        }
    }

    private void SpawnCustomer()
    {
        if (customerPrefabs == null || customerPrefabs.Count == 0 || spawnPoint == null || exitPoint == null || marketArea == null) return;

        List<CustomerAI.RequestedItem> generatedRequests = GenerateRandomRequestsForCustomer();

        int randomVariantIndex = Random.Range(0, customerPrefabs.Count);
        GameObject selectedPrefabVariant = customerPrefabs[randomVariantIndex];

        GameObject customerObj = Instantiate(selectedPrefabVariant, spawnPoint.position, Quaternion.identity, marketArea.transform);

        if (customerObj.TryGetComponent(out CustomerAI customerAI))
        {
            activeCustomers.Add(customerAI);
            marketArea.AddCustomer(customerAI);

            int index = marketArea.customerQueue.Count - 1;
            int pointIndex = Mathf.Min(index, marketArea.queuePoints.Count - 1);
            Transform targetQueuePoint = marketArea.queuePoints[pointIndex];
            customerAI.Initialize(marketArea, index, targetQueuePoint, exitPoint, generatedRequests);
        }
    }

    public void RemoveCustomerFromList(CustomerAI customer)
    {
        if (activeCustomers.Contains(customer))
        {
            activeCustomers.Remove(customer);
        }
    }

    private List<CustomerAI.RequestedItem> GenerateRandomRequestsForCustomer()
    {
        List<CustomerAI.RequestedItem> requests = new List<CustomerAI.RequestedItem>();

        if (availableItemTypes == null || availableItemTypes.Count == 0) return requests;

        int typesToRequestCount = Random.Range(minDifferentItemTypes, maxDifferentItemTypes + 1);
        typesToRequestCount = Mathf.Min(typesToRequestCount, availableItemTypes.Count);

        cachedItemPool.Clear();
        for (int i = 0; i < availableItemTypes.Count; i++)
        {
            cachedItemPool.Add(availableItemTypes[i]);
        }

        for (int i = 0; i < typesToRequestCount; i++)
        {
            int randomIndex = Random.Range(0, cachedItemPool.Count);
            ItemType selectedItemType = cachedItemPool[randomIndex];
            cachedItemPool.RemoveAt(randomIndex);

            int randomAmount = Random.Range(minAmountPerItem, maxAmountPerItem + 1);

            requests.Add(new CustomerAI.RequestedItem { itemType = selectedItemType, amount = randomAmount });
        }

        return requests;
    }
}
