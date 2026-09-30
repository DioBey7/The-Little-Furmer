using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class FactoryArea : MonoBehaviour
{
    [System.Serializable]
    public struct RequiredIngredient
    {
        public ItemType itemType;
        public int requiredAmount;
    }

    [HideInInspector] public List<RequiredIngredient> requiredIngredients = new List<RequiredIngredient>();
    public ItemType outputItemType;

    public Transform inputPoint;
    public List<Transform> outputPoints = new List<Transform>();
    public TMP_Text uiText;

    private Dictionary<ItemType, int> currentDeposited = new Dictionary<ItemType, int>();
    private Dictionary<ItemType, int> incomingItems = new Dictionary<ItemType, int>();
    private Dictionary<Transform, GameObject> outputOccupancy = new Dictionary<Transform, GameObject>();

    private bool isProcessing = false;
    private float currentSpeedMultiplier = 1f;

    private float cachedProcessingDuration = 3f;
    private string cachedOutputTag;
    private Vector3 cachedTargetScale = Vector3.one;

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            cachedProcessingDuration = LevelManager.Instance.CurrentLevelConfig.factorySettings.productionTimePerItem;

            List<FactorySettings.FactoryRecipeConfig> recipes = LevelManager.Instance.CurrentLevelConfig.factorySettings.factoryRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].factoryOutputType == outputItemType)
                {
                    requiredIngredients.Clear();
                    for (int j = 0; j < recipes[i].requiredIngredients.Count; j++)
                    {
                        RequiredIngredient ri = new RequiredIngredient();
                        ri.itemType = recipes[i].requiredIngredients[j].itemType;
                        ri.requiredAmount = recipes[i].requiredIngredients[j].requiredAmount;
                        requiredIngredients.Add(ri);
                    }
                    break;
                }
            }
        }

        cachedOutputTag = outputItemType.ToString();

        if (ItemManager.Instance != null)
        {
            ItemData refItem = ItemManager.Instance.GetItemByType(outputItemType);
            if (refItem != null && refItem.itemPrefab != null) cachedTargetScale = refItem.itemPrefab.transform.localScale;
        }

        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            ItemType type = requiredIngredients[i].itemType;
            if (!currentDeposited.ContainsKey(type))
            {
                currentDeposited[type] = 0;
                incomingItems[type] = 0;
            }
        }

        for (int i = 0; i < outputPoints.Count; i++)
        {
            outputOccupancy[outputPoints[i]] = null;
        }

        UpdateUI();
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        currentSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public bool NeedsItem(ItemType type)
    {
        if (isProcessing) return false;

        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            if (requiredIngredients[i].itemType == type)
            {
                int totalExpected = currentDeposited[type] + incomingItems[type];
                if (totalExpected < requiredIngredients[i].requiredAmount) return true;
            }
        }
        return false;
    }

    public void NotifyItemIncoming(ItemType type)
    {
        if (incomingItems.ContainsKey(type)) incomingItems[type]++;
    }

    public void OnItemReceived(ItemType type, GameObject itemObj)
    {
        if (incomingItems.TryGetValue(type, out int incCount) && incCount > 0) incomingItems[type]--;

        if (currentDeposited.ContainsKey(type))
        {
            currentDeposited[type]++;
            UpdateUI();
        }

        itemObj.transform.DOScale(Vector3.zero, 0.2f).OnComplete(() =>
        {
            if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(type, itemObj);
            else itemObj.SetActive(false);

            CheckAndStartProcessing();
        });
    }

    private void CheckAndStartProcessing()
    {
        if (isProcessing) return;

        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            if (currentDeposited[requiredIngredients[i].itemType] < requiredIngredients[i].requiredAmount) return;
        }

        if (!HasEmptyOutputPoint()) return;

        StartCoroutine(ProcessRoutine());
    }

    private IEnumerator ProcessRoutine()
    {
        isProcessing = true;

        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            currentDeposited[requiredIngredients[i].itemType] -= requiredIngredients[i].requiredAmount;
        }
        UpdateUI();

        yield return new WaitForSeconds(cachedProcessingDuration / currentSpeedMultiplier);

        SpawnOutput();

        isProcessing = false;
        UpdateUI();

        CheckAndStartProcessing();
    }

    private bool HasEmptyOutputPoint()
    {
        for (int i = 0; i < outputPoints.Count; i++)
        {
            if (outputOccupancy[outputPoints[i]] == null) return true;
        }
        return false;
    }

    private void SpawnOutput()
    {
        Transform emptyPoint = null;
        for (int i = 0; i < outputPoints.Count; i++)
        {
            if (outputOccupancy[outputPoints[i]] == null)
            {
                emptyPoint = outputPoints[i];
                break;
            }
        }

        if (emptyPoint == null || PoolingManager.Instance == null) return;

        GameObject outputItem = PoolingManager.Instance.GetItem(outputItemType);
        if (outputItem == null) return;

        if (SoundManager.Instance != null && SoundManager.Instance.produceClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.produceClip);
        }

        outputItem.transform.DOKill();

        if (outputItem.TryGetComponent(out Item itemComp))
        {
            itemComp.isCollected = false;
            itemComp.SetCollidersState(false);
            itemComp.OriginFactory = this;
            itemComp.OriginFactoryPoint = emptyPoint;
        }

        outputItem.tag = cachedOutputTag;
        outputItem.transform.SetParent(emptyPoint, true);
        outputOccupancy[emptyPoint] = outputItem;

        if (outputItem.TryGetComponent(out Rigidbody rb))
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (FxManager.Instance != null)
        {
            FxManager.Instance.PlayFx("FactoryProduce", emptyPoint.position);
        }

        outputItem.transform.localScale = Vector3.zero;
        outputItem.transform.position = emptyPoint.position;
        outputItem.transform.localRotation = Quaternion.identity;
        outputItem.transform.DOJump(emptyPoint.position, 1.5f, 1, 0.5f).SetEase(Ease.OutQuad);
        outputItem.transform.DOScale(cachedTargetScale, 0.5f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            if (outputItem == null) return;
            if (itemComp != null) itemComp.SetCollidersState(true);
            if (rb != null) rb.isKinematic = false;
        });

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.ProduceItem, 1, outputItemType);
        }

        // TUTO SİNYALİ BURAYA EKLENDİ
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnItemProduced();
        }
    }

    public void ReportItemCollected(Transform point)
    {
        if (point != null && outputOccupancy.ContainsKey(point))
        {
            outputOccupancy[point] = null;
            CheckAndStartProcessing();
        }
    }

    private void UpdateUI()
    {
        if (UiManager.Instance != null)
        {
            UiManager.Instance.UpdateFactoryWorldUI(uiText, isProcessing, requiredIngredients, currentDeposited);
        }
    }

    public void ResetFactory()
    {
        StopAllCoroutines();
        isProcessing = false;

        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            ItemType key = requiredIngredients[i].itemType;
            currentDeposited[key] = 0;
            incomingItems[key] = 0;
        }

        for (int i = 0; i < outputPoints.Count; i++)
        {
            Transform key = outputPoints[i];

            if (key == null) continue;

            if (outputOccupancy.TryGetValue(key, out GameObject occ))
            {
                if (occ != null)
                {
                    occ.transform.DOKill(true);
                    if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(outputItemType, occ);
                    else Destroy(occ);
                }
                outputOccupancy[key] = null;
            }
        }
        UpdateUI();
    }
}
