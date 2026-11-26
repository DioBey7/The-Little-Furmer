using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CashManager : MonoBehaviour
{
    public static CashManager Instance { get; private set; }

    [SerializeField] private GameObject cashPrefab;
    private List<GameObject> pool;
    private HashSet<GameObject> pooledObjects = new HashSet<GameObject>();

    public int defaultCashValue = 100;

    private Transform poolContainer;
    private Vector3 cachedScale;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        pool = new List<GameObject>(50);
        poolContainer = new GameObject("Cash_Pool").transform;
        poolContainer.SetParent(transform);

        if (cashPrefab != null) cachedScale = cashPrefab.transform.localScale;
    }

    private void Start()
    {
        if (cashPrefab == null) return;

        for (int i = 0; i < 20; i++)
        {
            GameObject cash = Instantiate(cashPrefab, poolContainer);
            cash.SetActive(false);
            AddToPool(cash);
        }
    }

    public GameObject GetCash(Vector3 position, Quaternion rotation)
    {
        GameObject cash = null;

        while (pool.Count > 0 && cash == null)
        {
            int lastIndex = pool.Count - 1;
            cash = pool[lastIndex];
            pool.RemoveAt(lastIndex);
            pooledObjects.Remove(cash);
        }

        if (cash != null)
        {
            if (cash.transform.parent != poolContainer) cash.transform.SetParent(poolContainer);

            cash.transform.SetPositionAndRotation(position, rotation);
            cash.SetActive(true);
        }
        else
        {
            cash = Instantiate(cashPrefab, position, rotation, poolContainer);
        }

        cash.transform.localScale = cachedScale;

        if (pool.Count < 5)
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject cashtemp = Instantiate(cashPrefab, poolContainer);
                cashtemp.SetActive(false);
                AddToPool(cashtemp);
            }
        }

        return cash;
    }

    public void ReturnToPool(GameObject cashObject)
    {
        if (cashObject == null) return;

        // Already pooled: returning it again would let GetCash hand it out twice.
        if (pooledObjects.Contains(cashObject))
        {
            if (cashObject.activeSelf) cashObject.SetActive(false);
            return;
        }

        cashObject.transform.DOKill(false);

        cashObject.SetActive(false);
        if (cashObject.transform.parent != poolContainer)
        {
            cashObject.transform.SetParent(poolContainer);
        }
        AddToPool(cashObject);
    }

    public bool IsPooled(GameObject cashObject)
    {
        return cashObject != null && pooledObjects.Contains(cashObject);
    }

    private void AddToPool(GameObject cashObject)
    {
        if (pooledObjects.Add(cashObject)) pool.Add(cashObject);
    }

    public Transform GetPoolContainer()
    {
        return poolContainer;
    }
}
