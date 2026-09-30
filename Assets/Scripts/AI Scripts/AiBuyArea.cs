using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class AiBuyArea : MonoBehaviour, IInteractableArea
{
    public GameObject aiWorkerPrefab;
    public Transform spawnPoint;
    public ItemType assignedItemType = ItemType.Tomato;

    public TMP_Text costText;
    public Image fillCircleImage;

    private float currentFillTime = 0f;
    private bool hasFlashed = false;
    private int currentPurchaseCount = 0;

    private int cachedBaseCost = 1000;
    private float cachedCostMultiplier = 1.5f;
    private float cachedDuration = 1.5f;
    private int cachedMaxLimit = 5;
    private Transform cachedCostTextTransform;

    private Vector3 originalCircleScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            PlayerAndAiSettings config = LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings;
            cachedBaseCost = config.costToHireNewWorker;
            cachedDuration = config.interactionTimeToHireWorker;
            cachedMaxLimit = config.maximumWorkersAllowed;
        }

        if (costText != null)
        {
            cachedCostTextTransform = costText.transform;
            originalTextScale = cachedCostTextTransform.localScale;
        }

        if (fillCircleImage != null)
        {
            originalCircleScale = fillCircleImage.transform.localScale;
            fillCircleImage.fillAmount = 0f;
        }

        if (GameManager.Instance != null)
        {
            List<AiWorkerSaveData> workers = GameManager.Instance.Data.purchasedAiWorkers;
            for (int i = 0; i < workers.Count; i++)
            {
                if (workers[i].itemType == assignedItemType)
                {
                    currentPurchaseCount = workers[i].count;
                    break;
                }
            }

            for (int i = 0; i < currentPurchaseCount; i++)
            {
                SpawnWorker(false);
            }
        }

        UpdateUI();
    }

    private int GetCurrentCost()
    {
        return Mathf.RoundToInt(cachedBaseCost * Mathf.Pow(cachedCostMultiplier, currentPurchaseCount));
    }

    private int GetDynamicMaxLimit()
    {
        int dynamicLimit = 0;
        if (FieldManager.Instance != null && FieldManager.Instance.activeFields != null)
        {
            List<Field> fields = FieldManager.Instance.activeFields;
            for (int i = 0; i < fields.Count; i++)
            {
                Field mainField = fields[i];
                if (mainField == null || !mainField.gameObject.activeInHierarchy || mainField.cropAreas == null) continue;

                for (int c = 0; c < mainField.cropAreas.Count; c++)
                {
                    Field.CropArea area = mainField.cropAreas[c];
                    if (area.cachedUnlockArea != null && area.cachedUnlockArea.gameObject.activeInHierarchy) continue;
                    if (area.cachedItemType == assignedItemType) dynamicLimit++;
                }
            }
        }
        return Mathf.Min(dynamicLimit, cachedMaxLimit);
    }

    public void OnPlayerEnter(ItemCollecting player)
    {
        if (player == null) return;
        hasFlashed = false;
        ResetPurchase();
    }

    public void OnPlayerExit(ItemCollecting player)
    {
        if (player == null) return;
        hasFlashed = false;
        ResetPurchase();
    }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (player == null) return;

        int currentMaxLimit = GetDynamicMaxLimit();

        if (currentPurchaseCount >= currentMaxLimit)
        {
            if (costText != null && costText.text != "MAX") UpdateUI();
            return;
        }

        int currentCost = GetCurrentCost();
        currentFillTime += deltaTime;
        float progress = Mathf.Clamp01(currentFillTime / cachedDuration);

        if (fillCircleImage != null) fillCircleImage.fillAmount = progress;

        if (currentFillTime >= cachedDuration)
        {
            if (UiManager.Instance == null || UiManager.Instance.CurrentCash < currentCost)
            {
                if (!hasFlashed && costText != null && cachedCostTextTransform != null && !DOTween.IsTweening(cachedCostTextTransform))
                {
                    hasFlashed = true;
                    cachedCostTextTransform.DOPunchScale(originalTextScale * 0.3f, 0.3f, 5, 1f);
                    costText.color = Color.red;
                    costText.text = "<size=65%>NOT ENOUGH CASH</size>";

                    DOVirtual.DelayedCall(1.0f, () =>
                    {
                        if (costText != null)
                        {
                            costText.color = Color.white;
                            UpdateUI();
                        }
                    });
                }
                ResetPurchase();
            }
            else
            {
                CompletePurchase(currentCost);
            }
        }
    }

    private void UpdateUI()
    {
        if (costText != null)
        {
            costText.transform.localScale = originalTextScale;
        }

        int currentMaxLimit = GetDynamicMaxLimit();

        if (currentPurchaseCount >= currentMaxLimit)
        {
            if (costText != null)
            {
                costText.text = "MAX";
                costText.color = Color.yellow;
            }
            if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;
            return;
        }

        if (costText != null && !hasFlashed)
        {
            costText.text = "$" + UiManager.FormatMoney(GetCurrentCost());
        }

        if (fillCircleImage != null && fillCircleImage.fillAmount == 0f)
        {
            fillCircleImage.fillAmount = 0f;
        }
    }

    private void ResetPurchase()
    {
        currentFillTime = 0f;
        if (fillCircleImage != null)
        {
            fillCircleImage.DOKill();
            fillCircleImage.fillAmount = 0f;
            fillCircleImage.color = Color.white;
            fillCircleImage.transform.localScale = originalCircleScale;
        }
        UpdateUI();
    }

    private void CompletePurchase(int cost)
    {
        if (UiManager.Instance != null && UiManager.Instance.CurrentCash >= cost)
        {
            UiManager.Instance.SpendCash(cost);
            UiManager.Instance.AddNotification("-$" + UiManager.FormatMoney(cost), Color.red);

            currentPurchaseCount++;

            if (GameManager.Instance != null)
            {
                List<AiWorkerSaveData> workers = GameManager.Instance.Data.purchasedAiWorkers;
                bool isFound = false;

                for (int i = 0; i < workers.Count; i++)
                {
                    if (workers[i].itemType == assignedItemType)
                    {
                        workers[i].count = currentPurchaseCount;
                        isFound = true;
                        break;
                    }
                }

                if (!isFound)
                {
                    workers.Add(new AiWorkerSaveData { itemType = assignedItemType, count = currentPurchaseCount });
                }

                GameManager.Instance.SaveData();
            }

            SpawnWorker(true);

            currentFillTime = 0f;
            if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;
            UpdateUI();

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnAiPurchased();
        }
    }

    private void SpawnWorker(bool playFx)
    {
        if (aiWorkerPrefab != null && spawnPoint != null)
        {
            GameObject aiObj = Instantiate(aiWorkerPrefab, spawnPoint.position, Quaternion.identity);

            if (aiObj.TryGetComponent(out AiWorker worker))
            {
                worker.SetTargetItemType(assignedItemType);
            }

            if (playFx)
            {
                if (SoundManager.Instance != null && SoundManager.Instance.buyAiClip != null)
                {
                    SoundManager.Instance.PlaySFX(SoundManager.Instance.buyAiClip);
                }

                if (FxManager.Instance != null)
                {
                    FxManager.Instance.PlayFx("LevelUp", transform.position + Vector3.up);
                }
            }
        }
    }
}
