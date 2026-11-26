using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class StockArea : MonoBehaviour
{
    [System.Serializable]
    public struct StockedItemData
    {
        public string pointName;
        public ItemType itemType;
        public GameObject itemPrefab;
    }

    public int stockID = 1;
    public GameObject acceptedItemPrefab;

    public ItemType AcceptedItemType { get; private set; }
    public ItemType expectedItemType => AcceptedItemType;
    public bool isTypeCached { get; private set; } = false;

    [SerializeField] private TMP_Text stockNameText;
    [SerializeField] private List<Transform> stockPoints = new List<Transform>();
    [SerializeField] private List<StockedItemData> stockedItemsInspectorList = new List<StockedItemData>();
    [SerializeField] private Transform cashSpawnPoint;

    private Dictionary<Transform, bool> pointOccupancy = new Dictionary<Transform, bool>();
    private int occupiedCount = 0;

    private Transform cachedPlayerTransform;
    public Transform fillPoint;
    private int cachedBaseCapacity = 4;
    private int cachedCapacityPerLevel = 1;
    private float cachedActionSpeed = 1.2f;

    private int cachedActiveCapacity = -1;

    private void Awake()
    {
        if (acceptedItemPrefab != null)
        {
            if (acceptedItemPrefab.TryGetComponent(out Item itemComp))
            {
                AcceptedItemType = itemComp.ItemType;
                isTypeCached = true;
            }
        }

        if (stockNameText != null && isTypeCached)
        {
            stockNameText.text = AcceptedItemType.ToString() + "\nStock";
        }
    }

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            StockAndMarketSettings config = LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings;
            cachedBaseCapacity = config.startingDisplayCapacity;
            cachedCapacityPerLevel = config.capacityIncreasePerLevel;
            cachedActionSpeed = config.itemTransferDelayToStock;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) cachedPlayerTransform = playerObj.transform;

        UpdateCachedCapacity();
        RefreshVisuals();
    }

    public void UpdateCachedCapacity()
    {
        int upgradeLevel = 0;
        if (GameManager.Instance != null)
        {
            List<StockSaveData> upgrades = GameManager.Instance.Data.stockUpgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i].stockID == this.stockID)
                {
                    upgradeLevel = upgrades[i].level;
                    break;
                }
            }
        }
        cachedActiveCapacity = Mathf.Clamp(cachedBaseCapacity + (upgradeLevel * cachedCapacityPerLevel), cachedBaseCapacity, stockPoints.Count);
    }

    public int GetActiveCapacity()
    {
        return cachedActiveCapacity;
    }

    public void RefreshVisuals()
    {
        UpdateCachedCapacity();
        int activeCap = GetActiveCapacity();

        Dictionary<Transform, bool> parentActiveState = new Dictionary<Transform, bool>();

        for (int i = 0; i < stockPoints.Count; i++)
        {
            Transform currentPoint = stockPoints[i];
            if (currentPoint != null)
            {
                bool isActive = i < activeCap;

                currentPoint.gameObject.SetActive(isActive);

                Transform parentObj = currentPoint.parent;
                if (parentObj != null && parentObj != this.transform)
                {
                    if (!parentActiveState.ContainsKey(parentObj))
                    {
                        parentActiveState[parentObj] = false;
                    }

                    if (isActive)
                    {
                        parentActiveState[parentObj] = true;
                    }
                }

                if (!pointOccupancy.ContainsKey(currentPoint))
                {
                    pointOccupancy[currentPoint] = false;
                }
            }
        }

        foreach (var kvp in parentActiveState)
        {
            Transform parentObj = kvp.Key;
            bool shouldBeActive = kvp.Value;

            if (parentObj.TryGetComponent(out MeshRenderer pRend)) pRend.enabled = true;
            if (parentObj.TryGetComponent(out Collider pCol)) pCol.enabled = true;
            if (parentObj.TryGetComponent(out NavMeshObstacle pObs)) pObs.enabled = true;

            parentObj.gameObject.SetActive(shouldBeActive);
        }
    }

    public bool HasEmptyStockPoint()
    {
        return occupiedCount < GetActiveCapacity();
    }

    public Transform GetNextStockPoint()
    {
        int activeLimit = GetActiveCapacity();

        for (int i = 0; i < activeLimit; i++)
        {
            Transform point = stockPoints[i];
            if (pointOccupancy.TryGetValue(point, out bool isOccupied) && !isOccupied)
            {
                pointOccupancy[point] = true;
                occupiedCount++;
                return point;
            }
        }
        return null;
    }

    public void AddItemToInspectorList(Transform stockPoint, ItemType itemType, GameObject itemPrefab, GameObject itemObject)
    {
        StockedItemData newData = new StockedItemData
        {
            pointName = stockPoint.gameObject.name,
            itemType = itemType,
            itemPrefab = itemPrefab
        };
        stockedItemsInspectorList.Add(newData);

        SpawnCashPhysically(itemType);
        StartCoroutine(ProcessSoldItem(itemObject, itemType, stockPoint));
    }

    private System.Collections.IEnumerator ProcessSoldItem(GameObject itemObject, ItemType itemType, Transform stockPoint)
    {
        float currentDuration = cachedActionSpeed;

        if (GameManager.Instance != null)
        {
            float speedMultiplier = 1f + ((cachedActiveCapacity - cachedBaseCapacity) / (float)cachedCapacityPerLevel) * 0.25f;
            currentDuration = Mathf.Max(0.6f, cachedActionSpeed / speedMultiplier);
        }

        yield return new WaitForSeconds(currentDuration);

        if (itemObject != null)
        {
            itemObject.transform.DOKill();
            Sequence shrinkSeq = DOTween.Sequence().SetLink(itemObject, LinkBehaviour.KillOnDisable);

            Vector3 currentScale = itemObject.transform.localScale;
            Vector3 currentPos = itemObject.transform.position;

            shrinkSeq.Append(itemObject.transform.DOScale(currentScale * 1.3f, 0.15f).SetEase(Ease.OutBack));
            shrinkSeq.Join(itemObject.transform.DOMoveY(currentPos.y + 0.3f, 0.15f).SetEase(Ease.OutQuad));

            shrinkSeq.Append(itemObject.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack));
            shrinkSeq.Join(itemObject.transform.DORotate(new Vector3(0f, 360f, 0f), 0.25f, RotateMode.FastBeyond360).SetRelative(true));
            shrinkSeq.Join(itemObject.transform.DOMoveY(currentPos.y + 0.8f, 0.25f).SetEase(Ease.InBack));

            shrinkSeq.OnComplete(() =>
            {
                if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(itemType, itemObject);
                else itemObject.SetActive(false);
            });

            yield return shrinkSeq.WaitForCompletion();
        }

        for (int i = 0; i < stockedItemsInspectorList.Count; i++)
        {
            if (stockedItemsInspectorList[i].pointName == stockPoint.gameObject.name)
            {
                stockedItemsInspectorList.RemoveAt(i);
                break;
            }
        }

        if (pointOccupancy.ContainsKey(stockPoint)) pointOccupancy[stockPoint] = false;
        occupiedCount = Mathf.Max(0, occupiedCount - 1);
    }

    private void SpawnCashPhysically(ItemType itemType)
    {
        if (CashManager.Instance == null || cashSpawnPoint == null) return;

        ItemData itemData = ItemManager.Instance != null ? ItemManager.Instance.GetItemByType(itemType) : null;
        int spawnCount = itemData != null ? itemData.CashSpawnCount : 1;
        int cashValue = CashManager.Instance != null ? CashManager.Instance.defaultCashValue : 100;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 safeSpawnPosition = cashSpawnPoint.position + (Vector3.up * 0.5f);
            GameObject cash = CashManager.Instance.GetCash(safeSpawnPosition, Quaternion.identity);

            cash.transform.SetParent(null, true);
            if (cash.TryGetComponent(out CashItem cashComp)) cashComp.Initialize(cashValue);

            cash.transform.DOKill();

            Vector3 targetScale = cash.transform.localScale;
            if (targetScale == Vector3.zero) targetScale = Vector3.one;

            Vector3 targetPosition;
            if (cachedPlayerTransform != null)
            {
                Vector3 dirToPlayer = (cachedPlayerTransform.position - cashSpawnPoint.position);
                dirToPlayer.y = 0;

                Vector3 forwardDir = dirToPlayer.sqrMagnitude > 0.01f ? dirToPlayer.normalized : cashSpawnPoint.forward;
                Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir);

                float forwardDistance = Random.Range(1.75f, 2.75f);
                float sideDistance = Random.Range(-1f, 1f);

                targetPosition = cashSpawnPoint.position + (forwardDir * forwardDistance) + (rightDir * sideDistance);
            }
            else
            {
                targetPosition = cashSpawnPoint.position + (cashSpawnPoint.forward * Random.Range(2f, 6f)) + (cashSpawnPoint.right * Random.Range(-0.5f, 0.5f));
            }

            Vector3 rayStart = new Vector3(targetPosition.x, cashSpawnPoint.position.y + 5f, targetPosition.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                targetPosition.y = hit.point.y + 0.05f;
            }
            else targetPosition.y = 0f;

            cash.transform.position = safeSpawnPosition;
            cash.transform.localScale = Vector3.zero;

            float jumpPower = Random.Range(0.8f, 1.2f);
            float jumpDuration = Random.Range(0.4f, 0.8f);

            cash.transform.DOJump(targetPosition, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
            cash.transform.DORotate(new Vector3(0f, Random.Range(180f, 360f), 0f), jumpDuration, RotateMode.FastBeyond360);

            Sequence scaleSeq = DOTween.Sequence().SetLink(cash, LinkBehaviour.KillOnDisable);
            scaleSeq.Append(cash.transform.DOScale(targetScale * 1.15f, jumpDuration * 0.5f));
            scaleSeq.Append(cash.transform.DOScale(targetScale, jumpDuration * 0.5f).SetEase(Ease.OutBack));
        }
    }

    public void ResetStock()
    {
        StopAllCoroutines();
        stockedItemsInspectorList.Clear();
        occupiedCount = 0;

        for (int i = 0; i < stockPoints.Count; i++)
        {
            Transform point = stockPoints[i];
            if (point != null)
            {
                pointOccupancy[point] = false;

                for (int j = point.childCount - 1; j >= 0; j--)
                {
                    Transform child = point.GetChild(j);
                    child.DOKill(false);

                    if (child.TryGetComponent(out Item itemComp))
                    {
                        if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(itemComp.ItemType, child.gameObject);
                        else child.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
