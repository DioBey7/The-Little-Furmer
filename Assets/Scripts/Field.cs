using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class Field : MonoBehaviour
{
    [System.Serializable]
    public class CropArea
    {
        [HideInInspector] public int autoFieldID;
        public Transform areaRoot;
        public GameObject fieldItemPrefab;
        public TMP_Text localDirtCounterText;
        public List<Transform> dirtPoints = new List<Transform>();

        [HideInInspector] public UnlockArea cachedUnlockArea;

        [HideInInspector] public Dictionary<Transform, GameObject> dirtOccupancy = new Dictionary<Transform, GameObject>();
        [HideInInspector] public int currentReadyCount = 0;
        [HideInInspector] public ItemType cachedItemType;
        [HideInInspector] public string cachedItemTag;
        [HideInInspector] public bool isTypeCached = false;
        [HideInInspector] public Coroutine productionCoroutine;
    }

    public List<CropArea> cropAreas = new List<CropArea>();

    private float cachedGrowthDuration = 3f;

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            cachedGrowthDuration = LevelManager.Instance.CurrentLevelConfig.fieldSettings.cropGrowthTimeInSeconds;
        }

        for (int c = 0; c < cropAreas.Count; c++)
        {
            CropArea area = cropAreas[c];

            area.autoFieldID = c;

            area.cachedUnlockArea = area.areaRoot != null ? area.areaRoot.GetComponentInParent<UnlockArea>(true) : null;

            if (area.fieldItemPrefab != null && area.fieldItemPrefab.TryGetComponent(out Item itemComp))
            {
                area.cachedItemType = itemComp.ItemType;
                area.cachedItemTag = area.cachedItemType.ToString();
                area.isTypeCached = true;

                if (PoolingManager.Instance != null)
                {
                    PoolingManager.Instance.PrewarmPoolForType(area.cachedItemType, area.dirtPoints.Count);
                }
            }

            for (int i = 0; i < area.dirtPoints.Count; i++)
            {
                if (area.dirtPoints[i] != null && !area.dirtOccupancy.ContainsKey(area.dirtPoints[i]))
                {
                    area.dirtOccupancy[area.dirtPoints[i]] = null;
                }
            }
        }

        RefreshVisuals();
        InitializeField();
    }

    private void OnEnable()
    {
        for (int c = 0; c < cropAreas.Count; c++)
        {
            if (cropAreas[c].isTypeCached && cropAreas[c].productionCoroutine == null)
            {
                cropAreas[c].productionCoroutine = StartCoroutine(MasterProductionRoutine(cropAreas[c]));
            }
        }
    }

    private void OnDisable()
    {
        for (int c = 0; c < cropAreas.Count; c++)
        {
            if (cropAreas[c].productionCoroutine != null)
            {
                StopCoroutine(cropAreas[c].productionCoroutine);
                cropAreas[c].productionCoroutine = null;
            }
        }
    }

    public int GetCurrentUpgradeLevel(int fID)
    {
        if (GameManager.Instance == null || GameManager.Instance.Data.fieldUpgrades == null) return 0;
        List<StockSaveData> upgrades = GameManager.Instance.Data.fieldUpgrades;
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].stockID == fID) return upgrades[i].level;
        }
        return 0;
    }

    public int GetActiveCapacity(CropArea area)
    {
        if (area.dirtPoints.Count <= 4) return area.dirtPoints.Count;
        int upgradeLevel = GetCurrentUpgradeLevel(area.autoFieldID);
        return Mathf.Clamp(3 + (upgradeLevel * 3), 3, area.dirtPoints.Count);
    }

    public CropArea GetCropAreaByUpgradeArea(Transform upgradeTransform)
    {
        for (int i = 0; i < cropAreas.Count; i++)
        {
            if (cropAreas[i].areaRoot != null && upgradeTransform.IsChildOf(cropAreas[i].areaRoot))
            {
                return cropAreas[i];
            }
        }
        return null;
    }

    public void RefreshVisuals()
    {
        for (int c = 0; c < cropAreas.Count; c++)
        {
            CropArea area = cropAreas[c];
            int activeCap = GetActiveCapacity(area);

            for (int i = 0; i < area.dirtPoints.Count; i++)
            {
                if (area.dirtPoints[i] != null)
                {
                    bool isActive = i < activeCap;
                    area.dirtPoints[i].gameObject.SetActive(isActive);
                }
            }
            UpdateCountersUI(area);
        }
    }

    private void InitializeField()
    {
        for (int c = 0; c < cropAreas.Count; c++)
        {
            cropAreas[c].currentReadyCount = 0;
            UpdateCountersUI(cropAreas[c]);

            if (cropAreas[c].productionCoroutine == null)
            {
                cropAreas[c].productionCoroutine = StartCoroutine(MasterProductionRoutine(cropAreas[c]));
            }
        }
    }

    private IEnumerator MasterProductionRoutine(CropArea area)
    {
        while (true)
        {
            int targetIndex = GetLowestEmptyDirtIndex(area);

            if (targetIndex != -1)
            {
                yield return new WaitForSeconds(cachedGrowthDuration);

                Transform targetDirt = area.dirtPoints[targetIndex];

                if (area.dirtOccupancy.TryGetValue(targetDirt, out GameObject occ) && occ == null)
                {
                    SpawnItemOnDirt(area, targetDirt, false);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    private int GetLowestEmptyDirtIndex(CropArea area)
    {
        int currentLimit = GetActiveCapacity(area);
        for (int i = 0; i < currentLimit; i++)
        {
            if (i >= area.dirtPoints.Count) break;

            Transform dirt = area.dirtPoints[i];

            if (dirt == null || !dirt.gameObject.activeInHierarchy) continue;

            if (area.dirtOccupancy.TryGetValue(dirt, out GameObject occ) && occ == null)
            {
                return i;
            }
        }
        return -1;
    }

    private void SpawnItemOnDirt(CropArea area, Transform dirt, bool instant)
    {
        if (dirt == null || !area.isTypeCached || !dirt.gameObject.activeInHierarchy) return;

        if (area.dirtOccupancy.TryGetValue(dirt, out GameObject existing) && existing != null && existing.activeSelf)
            return;

        if (PoolingManager.Instance == null) return;

        GameObject item = PoolingManager.Instance.GetItem(area.cachedItemType);
        if (item == null) return;

        if (item.TryGetComponent(out Item itemComp))
        {
            itemComp.OriginDirtPoint = dirt;
            itemComp.isCollected = false;

            item.tag = area.cachedItemTag;
            itemComp.SetCollidersState(false);

            item.transform.SetParent(this.transform, true);
            item.transform.position = dirt.position;
            item.transform.rotation = area.fieldItemPrefab != null ? area.fieldItemPrefab.transform.rotation : Quaternion.identity;

            area.dirtOccupancy[dirt] = item;

            Vector3 prefabScale = area.fieldItemPrefab != null ? area.fieldItemPrefab.transform.localScale : Vector3.one;

            if (instant)
            {
                item.transform.localScale = prefabScale;
                itemComp.SetCollidersState(true);
                IncrementReadyCount(area);
            }
            else
            {
                item.transform.localScale = Vector3.zero;
                float popAnimDuration = 0.4f;

                item.transform.DOScale(prefabScale, popAnimDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        if (item == null) return;
                        item.transform.localScale = prefabScale;

                        itemComp.SetCollidersState(true);
                        IncrementReadyCount(area);

                        if (FxManager.Instance != null && gameObject.activeInHierarchy)
                        {
                            FxManager.Instance.PlayFx("CropReady", dirt.position + (Vector3.up * 0.1f));
                        }
                    });
            }
        }
    }

    public void ReportItemCollected(GameObject itemObject)
    {
        if (itemObject == null) return;

        if (itemObject.TryGetComponent(out Item itemComp) && itemComp.OriginDirtPoint != null)
        {
            Transform dirt = itemComp.OriginDirtPoint;
            itemComp.OriginDirtPoint = null;

            for (int c = 0; c < cropAreas.Count; c++)
            {
                CropArea area = cropAreas[c];
                if (area.dirtOccupancy.ContainsKey(dirt))
                {
                    area.dirtOccupancy[dirt] = null;
                    DecrementReadyCount(area);
                    break;
                }
            }
        }
    }

    private void IncrementReadyCount(CropArea area)
    {
        int currentLimit = GetActiveCapacity(area);
        area.currentReadyCount = Mathf.Min(area.currentReadyCount + 1, currentLimit);
        UpdateCountersUI(area);
    }

    private void DecrementReadyCount(CropArea area)
    {
        area.currentReadyCount = Mathf.Max(0, area.currentReadyCount - 1);
        UpdateCountersUI(area);
    }

    private void UpdateCountersUI(CropArea area)
    {
        if (area.localDirtCounterText != null)
        {
            int currentLimit = GetActiveCapacity(area);
            area.localDirtCounterText.text = (area.currentReadyCount >= currentLimit && currentLimit > 0) ? "FULL" : $"{area.currentReadyCount.ToString()}/{currentLimit.ToString()}";
        }
    }

    public void ResetField()
    {
        for (int c = 0; c < cropAreas.Count; c++)
        {
            CropArea area = cropAreas[c];
            if (area.productionCoroutine != null)
            {
                StopCoroutine(area.productionCoroutine);
                area.productionCoroutine = null;
            }

            List<Transform> keys = new List<Transform>(area.dirtOccupancy.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                area.dirtOccupancy[keys[i]] = null;
            }

            area.currentReadyCount = 0;
        }

        RefreshVisuals();

        for (int c = 0; c < cropAreas.Count; c++)
        {
            cropAreas[c].productionCoroutine = StartCoroutine(MasterProductionRoutine(cropAreas[c]));
        }
    }
}
