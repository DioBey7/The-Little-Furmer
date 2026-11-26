using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ItemCollecting : MonoBehaviour
{
    [System.Serializable]
    public struct BackpackItemData
    {
        public ItemType itemType;
        public GameObject prefab;
    }

    [SerializeField] private Transform backPack;
    [SerializeField] private float itemSpacingBuffer = 0.10f;
    [SerializeField] private List<BackpackItemData> backpackInspectorList = new List<BackpackItemData>();

    private List<Item> collectedItemsList = new List<Item>();
    private float currentBackpackHeight = 0f;
    private FieldManager fieldManagerReference;
    private float nextCapacityNotificationTime = 0f;

    public int CollectedCount => collectedItemsList.Count;

    private void Start()
    {
        fieldManagerReference = FieldManager.Instance;
    }

    public int GetMaxCapacity()
    {
        int baseCap = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.startingBackpackCapacity : 5;
        int capPerLevel = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.capacityIncreasePerBackpackLevel : 3;

        int upgradeLevel = GameManager.Instance != null ? GameManager.Instance.Data.backpackUpgradeLevel : 0;
        return baseCap + (upgradeLevel * capPerLevel);
    }

    public void CollectCash(GameObject cashObj)
    {
        if (cashObj.TryGetComponent(out CashItem cashItem))
        {
            if (UiManager.Instance != null)
            {
                UiManager.Instance.AddCash(cashItem.CashValue);
                UiManager.Instance.AddNotification("+$" + UiManager.FormatMoney(cashItem.CashValue), Color.green);
            }

            if (SoundManager.Instance != null) SoundManager.Instance.PlayCashPickupSound();

            if (FxManager.Instance != null)
            {
                FxManager.Instance.PlayFx("CashCollect", cashObj.transform.position + (Vector3.up * 1f));
            }

            if (CashManager.Instance != null) CashManager.Instance.ReturnToPool(cashObj);
        }
    }

    public void CollectItem(GameObject itemObj, Item itemComp)
    {
        if (collectedItemsList.Count >= GetMaxCapacity())
        {
            if (Time.time >= nextCapacityNotificationTime)
            {
                if (UiManager.Instance != null) UiManager.Instance.AddNotification("MAX CAPACITY!", Color.red);
                nextCapacityNotificationTime = Time.time + 2f;
            }
            return;
        }

        itemComp.isCollected = true;
        itemObj.tag = "Untagged";

        if (itemObj.TryGetComponent(out Collider col)) col.enabled = false;

        bool isRawCrop = (itemComp.OriginDirtPoint != null);

        if (UiManager.Instance != null)
        {
            UiManager.Instance.IncrementItem(itemComp.ItemType);
            UiManager.Instance.AddNotification("+1 " + itemComp.ItemType.ToString(), Color.white);
        }

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.HarvestItem, 1, itemComp.ItemType);
        }

        if (fieldManagerReference != null) fieldManagerReference.ReportItemCollected(itemObj);

        if (itemComp.OriginFactory != null)
        {
            itemComp.OriginFactory.ReportItemCollected(itemComp.OriginFactoryPoint);
            itemComp.OriginFactory = null;
            itemComp.OriginFactoryPoint = null;
        }

        if (isRawCrop && FxManager.Instance != null)
        {
            FxManager.Instance.PlayFx("CropHarvest", itemObj.transform.position + (Vector3.up * 0.5f));
        }

        CollectItemToBackpack(itemComp);

        if (SoundManager.Instance != null) SoundManager.Instance.PlayItemPickupSound();
    }

    private float GetItemRealHeight(GameObject itemObj, Vector3 baseScale)
    {
        if (itemObj.TryGetComponent(out Collider col))
        {
            if (col is BoxCollider box) return box.size.y * baseScale.y;
            else if (col is SphereCollider sphere) return sphere.radius * 2f * baseScale.y;
            else if (col is CapsuleCollider capsule) return capsule.height * baseScale.y;
        }
        return baseScale.y;
    }

    private void CollectItemToBackpack(Item itemComponent)
    {
        GameObject itemObject = itemComponent.gameObject;

        if (itemObject.TryGetComponent(out Rigidbody itemRigidbody))
        {
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            itemRigidbody.isKinematic = true;
        }

        Vector3 prefabScale = itemComponent.ItemPrefab != null ? itemComponent.ItemPrefab.transform.localScale : Vector3.one;
        itemObject.transform.SetParent(backPack, true);

        float itemHeight = itemComponent.data != null ? itemComponent.data.stackHeight : GetItemRealHeight(itemObject, prefabScale);
        Vector3 localTargetPosition = new Vector3(0, currentBackpackHeight + (itemHeight / 2f), 0);
        currentBackpackHeight += itemHeight + itemSpacingBuffer;

        itemObject.transform.DOKill();
        float jumpDuration = 0.35f;

        itemObject.transform.DOLocalJump(localTargetPosition, 1.5f, 1, jumpDuration);

        Sequence scaleSeq = DOTween.Sequence().SetLink(itemObject, LinkBehaviour.KillOnDisable);
        scaleSeq.Append(itemObject.transform.DOScale(new Vector3(prefabScale.x * 0.7f, prefabScale.y * 1.3f, prefabScale.z * 0.7f), jumpDuration * 0.4f));
        scaleSeq.Append(itemObject.transform.DOScale(new Vector3(prefabScale.x * 1.3f, prefabScale.y * 0.7f, prefabScale.z * 1.3f), jumpDuration * 0.3f));
        scaleSeq.Append(itemObject.transform.DOScale(prefabScale, jumpDuration * 0.3f).SetEase(Ease.OutElastic));

        scaleSeq.OnComplete(() =>
        {
            if (itemObject == null) return;
            itemObject.transform.localPosition = localTargetPosition;
            itemObject.transform.localRotation = Quaternion.identity;
            itemObject.transform.localScale = prefabScale;

            backpackInspectorList.Add(new BackpackItemData { itemType = itemComponent.ItemType, prefab = itemComponent.ItemPrefab });

            if (backPack != null)
            {
                backPack.DOKill(true);
                backPack.localScale = Vector3.one;
                backPack.DOPunchScale(new Vector3(0.15f, -0.15f, 0.15f), 0.2f, 5, 1f);
            }
        });

        collectedItemsList.Add(itemComponent);

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnItemCollected();
    }

    public bool HasItem(ItemType itemType)
    {
        for (int i = collectedItemsList.Count - 1; i >= 0; i--)
        {
            if (collectedItemsList[i] != null && collectedItemsList[i].ItemType == itemType) return true;
        }
        return false;
    }

    public void ConsumeItem(ItemType itemType)
    {
        if (TryExtractItemFromBackpack(itemType, out GameObject itemObj, out _, out _, out _))
        {
            if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(itemType, itemObj);
            else Destroy(itemObj);
        }
    }

    private bool TryExtractItemFromBackpack(ItemType targetType, out GameObject itemObj, out Item itemComp, out float itemHeight, out Vector3 prefabScale)
    {
        itemObj = null; itemComp = null; itemHeight = 0f; prefabScale = Vector3.one;

        if (collectedItemsList.Count == 0) return false;

        int targetIndex = -1;
        for (int i = collectedItemsList.Count - 1; i >= 0; i--)
        {
            if (collectedItemsList[i] != null && collectedItemsList[i].ItemType == targetType)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex == -1) return false;

        itemComp = collectedItemsList[targetIndex];
        itemObj = itemComp.gameObject;
        collectedItemsList.RemoveAt(targetIndex);

        for (int i = 0; i < backpackInspectorList.Count; i++)
        {
            if (backpackInspectorList[i].itemType == targetType)
            {
                backpackInspectorList.RemoveAt(i);
                break;
            }
        }

        GameObject savedPrefab = itemComp.ItemPrefab;
        prefabScale = (savedPrefab != null) ? savedPrefab.transform.localScale : Vector3.one;
        itemHeight = itemComp.data != null ? itemComp.data.stackHeight : GetItemRealHeight(itemObj, prefabScale);

        ReorganizeBackpack();

        itemObj.transform.SetParent(null, true);
        itemObj.transform.DOKill();

        return true;
    }

    public void ExecuteSpecificUnloadSignal(StockArea stockArea, Transform targetStockPoint, ItemType expectedItemType)
    {
        if (targetStockPoint == null || stockArea == null) return;

        if (TryExtractItemFromBackpack(expectedItemType, out GameObject itemObj, out Item itemComp, out float itemHeight, out Vector3 prefabScale))
        {
            Vector3 targetPosition = targetStockPoint.position + new Vector3(0, itemHeight / 2f, 0);
            float jumpDuration = 0.4f;

            itemObj.transform.DOJump(targetPosition, 2f, 1, jumpDuration);
            PlayUnloadScaleTween(itemObj, prefabScale, jumpDuration, () =>
            {
                if (itemObj == null) return;
                itemObj.transform.position = targetPosition;
                itemObj.transform.rotation = targetStockPoint.rotation;
                stockArea.AddItemToInspectorList(targetStockPoint, expectedItemType, itemComp.ItemPrefab, itemObj);

                if (ObjectiveManager.Instance != null) ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.SellItem, 1, expectedItemType);
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnItemSold();
            });
        }
    }

    public void ExecuteFactoryUnloadSignal(FactoryArea factory, Transform targetPoint, ItemType expectedItemType)
    {
        if (targetPoint == null || factory == null) return;

        if (TryExtractItemFromBackpack(expectedItemType, out GameObject itemObj, out _, out _, out _))
        {
            itemObj.transform.DOJump(targetPoint.position, 2f, 1, 0.4f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (itemObj != null) factory.OnItemReceived(expectedItemType, itemObj);
            });
        }
    }

    public void ExecuteCustomerUnloadSignal(CustomerAI customer, Transform targetPoint, ItemType expectedItemType)
    {
        if (targetPoint == null || customer == null) return;

        if (TryExtractItemFromBackpack(expectedItemType, out GameObject itemObj, out _, out _, out _))
        {
            itemObj.transform.DOJump(targetPoint.position + Vector3.up * 1f, 2f, 1, 0.4f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (customer != null) customer.ReceiveItem(expectedItemType);
                if (itemObj != null)
                {
                    if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(expectedItemType, itemObj);
                    else Destroy(itemObj);
                }
            });
        }
    }

    private void PlayUnloadScaleTween(GameObject obj, Vector3 targetScale, float duration, TweenCallback onComplete)
    {
        Sequence scaleSeq = DOTween.Sequence().SetLink(obj, LinkBehaviour.KillOnDisable);
        scaleSeq.Append(obj.transform.DOScale(new Vector3(targetScale.x * 0.7f, targetScale.y * 1.3f, targetScale.z * 0.7f), duration * 0.4f));
        scaleSeq.Append(obj.transform.DOScale(new Vector3(targetScale.x * 1.3f, targetScale.y * 0.7f, targetScale.z * 1.3f), duration * 0.3f));
        scaleSeq.Append(obj.transform.DOScale(targetScale, duration * 0.3f).SetEase(Ease.OutElastic));
        scaleSeq.OnComplete(onComplete);
    }

    private void ReorganizeBackpack()
    {
        currentBackpackHeight = 0f;

        for (int i = 0; i < collectedItemsList.Count; i++)
        {
            Item itemComp = collectedItemsList[i];
            if (itemComp != null && itemComp.gameObject != null)
            {
                Vector3 baseScale = itemComp.ItemPrefab != null ? itemComp.ItemPrefab.transform.localScale : Vector3.one;
                float itemHeight = itemComp.data != null ? itemComp.data.stackHeight : GetItemRealHeight(itemComp.gameObject, baseScale);
                Vector3 newLocalPos = new Vector3(0, currentBackpackHeight + (itemHeight / 2f), 0);

                itemComp.transform.DOKill();
                itemComp.transform.DOLocalMove(newLocalPos, 0.2f).SetEase(Ease.OutQuad);

                currentBackpackHeight += itemHeight + itemSpacingBuffer;
            }
        }
    }

    public void ResetBackpack()
    {
        foreach (var item in collectedItemsList)
        {
            if (item != null)
            {
                item.transform.DOKill(true);
                if (PoolingManager.Instance != null) PoolingManager.Instance.ReturnItem(item.ItemType, item.gameObject);
                else Destroy(item.gameObject);
            }
        }
        collectedItemsList.Clear();
        backpackInspectorList.Clear();
        currentBackpackHeight = 0f;

        if (backPack != null)
        {
            backPack.DOKill(true);
            backPack.localScale = Vector3.one;
        }
    }
}
