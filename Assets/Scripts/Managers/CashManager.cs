using System.Collections.Generic;
using UnityEngine;

public class CashManager : MonoBehaviour
{
    public static CashManager Instance { get; private set; }

    [SerializeField] private GameObject cashPrefab;
    private List<GameObject> pool;

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
            pool.Add(cash);
        }
    }

    public GameObject GetCash(Vector3 position, Quaternion rotation)
    {
        GameObject cash;

        if (pool.Count > 0)
        {
            int lastIndex = pool.Count - 1;
            cash = pool[lastIndex];
            pool.RemoveAt(lastIndex);

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
                pool.Add(cashtemp);
            }
        }

        return cash;
    }

    public void ReturnToPool(GameObject cashObject)
    {
        if (cashObject == null) return;

        cashObject.SetActive(false);
        if (cashObject.transform.parent != poolContainer)
        {
            cashObject.transform.SetParent(poolContainer);
        }
        pool.Add(cashObject);
    }

    public Transform GetPoolContainer()
    {
        return poolContainer;
    }
}
