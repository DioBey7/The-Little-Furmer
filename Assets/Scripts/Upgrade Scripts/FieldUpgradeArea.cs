using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class FieldUpgradeArea : MonoBehaviour, IInteractableArea
{
    public Field targetFieldManager;
    private Field.CropArea myCropArea;

    public TMP_Text costText;
    public Image fillCircleImage;

    private float currentFillTime = 0f;
    private bool hasFlashed = false;
    private Transform cachedCostTextTransform;

    private int cachedBaseCost = 500;
    private float cachedCostMultiplier = 1.5f;
    private float cachedUpgradeFillDuration = 1.5f;

    private Vector3 originalCircleScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    private void Awake()
    {
        if (TryGetComponent(out BoxCollider col))
        {
            col.isTrigger = true;
            if (col.size == Vector3.one) col.size = new Vector3(2f, 2f, 2f);
        }

        if (fillCircleImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].type == Image.Type.Filled)
                {
                    fillCircleImage = images[i];
                    break;
                }
            }
        }
    }

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            FieldSettings config = LevelManager.Instance.CurrentLevelConfig.fieldSettings;
            cachedBaseCost = config.baseUpgradeCost;
            cachedCostMultiplier = config.upgradeCostMultiplier;
            cachedUpgradeFillDuration = Mathf.Max(0.5f, config.interactionTimeToUpgrade);
        }

        if (costText != null)
        {
            cachedCostTextTransform = costText.transform;
            originalTextScale = cachedCostTextTransform.localScale;
            if (originalTextScale == Vector3.zero) originalTextScale = Vector3.one;
        }

        if (fillCircleImage != null)
        {
            fillCircleImage.type = Image.Type.Filled;
            fillCircleImage.fillMethod = Image.FillMethod.Radial360;

            originalCircleScale = fillCircleImage.transform.localScale;
            if (originalCircleScale == Vector3.zero) originalCircleScale = Vector3.one;

            fillCircleImage.fillAmount = 0f;
            fillCircleImage.color = Color.white;
        }

        if (targetFieldManager != null)
        {
            myCropArea = targetFieldManager.GetCropAreaByUpgradeArea(this.transform);
        }

        UpdateUI();
    }

    public void OnPlayerEnter(ItemCollecting player)
    {
        hasFlashed = false;
        ResetPurchase();
    }

    public void OnPlayerExit(ItemCollecting player)
    {
        hasFlashed = false;
        ResetPurchase();
    }

    private int GetCurrentLevel()
    {
        if (GameManager.Instance == null || targetFieldManager == null || myCropArea == null) return 0;
        return targetFieldManager.GetCurrentUpgradeLevel(myCropArea.autoFieldID);
    }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (targetFieldManager == null) return;

        if (myCropArea == null) myCropArea = targetFieldManager.GetCropAreaByUpgradeArea(this.transform);
        if (myCropArea == null) return;

        int currentCapacity = targetFieldManager.GetActiveCapacity(myCropArea);

        if (currentCapacity >= myCropArea.dirtPoints.Count)
        {
            ResetPurchase();
            return;
        }

        int currentLevel = GetCurrentLevel();
        int currentCost = GetCurrentCost(currentLevel);

        currentFillTime += deltaTime;
        float progress = Mathf.Clamp01(currentFillTime / cachedUpgradeFillDuration);

        if (fillCircleImage != null) fillCircleImage.fillAmount = progress;

        if (currentFillTime >= cachedUpgradeFillDuration)
        {
            if (UiManager.Instance == null || UiManager.Instance.CurrentCash < currentCost)
            {
                if (!hasFlashed && costText != null && cachedCostTextTransform != null && !DOTween.IsTweening(cachedCostTextTransform))
                {
                    hasFlashed = true;
                    cachedCostTextTransform.DOPunchScale(originalTextScale * 0.3f, 0.3f, 5, 1f);
                    costText.color = Color.red;
                    DOVirtual.DelayedCall(0.5f, UpdateUI);
                }
                ResetPurchase();
            }
            else
            {
                CompleteUpgrade(currentLevel, currentCost);
            }
        }
    }

    private int GetCurrentCost(int level)
    {
        return Mathf.RoundToInt(cachedBaseCost * Mathf.Pow(cachedCostMultiplier, level));
    }

    private void UpdateUI()
    {
        if (targetFieldManager == null) return;
        if (myCropArea == null) myCropArea = targetFieldManager.GetCropAreaByUpgradeArea(this.transform);
        if (myCropArea == null) return;

        int currentCapacity = targetFieldManager.GetActiveCapacity(myCropArea);

        if (costText != null)
        {
            costText.DOKill();
            costText.color = Color.white;
            costText.transform.localScale = originalTextScale;

            if (currentCapacity >= myCropArea.dirtPoints.Count)
            {
                costText.text = "MAX";
                costText.color = Color.yellow;
            }
            else
            {
                int currentLevel = GetCurrentLevel();
                costText.text = "Upgrade Field\n" + "$" + UiManager.FormatMoney(GetCurrentCost(currentLevel));
            }
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
    }

    private void CompleteUpgrade(int currentLevel, int cost)
    {
        if (UiManager.Instance != null)
        {
            UiManager.Instance.SpendCash(cost);
            UiManager.Instance.AddNotification("-$" + cost.ToString(), Color.red);
            UiManager.Instance.AddNotification("FIELD EXPANDED!", Color.yellow);
        }

        if (SoundManager.Instance != null && SoundManager.Instance.upgradeClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.upgradeClip);
        }

        if (FxManager.Instance != null)
        {
            FxManager.Instance.PlayFx("LevelUp", transform.position + Vector3.up);
        }

        if (GameManager.Instance != null && targetFieldManager != null && myCropArea != null)
        {
            List<StockSaveData> upgrades = GameManager.Instance.Data.fieldUpgrades;
            StockSaveData data = upgrades.Find(x => x.stockID == myCropArea.autoFieldID);

            if (data != null) data.level = currentLevel + 1;
            else upgrades.Add(new StockSaveData { stockID = myCropArea.autoFieldID, level = currentLevel + 1 });

            GameManager.Instance.SaveData();
            targetFieldManager.RefreshVisuals();
        }

        hasFlashed = false;
        currentFillTime = 0f;
        UpdateUI();

        if (fillCircleImage != null)
        {
            fillCircleImage.DOKill();
            fillCircleImage.fillAmount = 1f;
            fillCircleImage.color = Color.white;

            fillCircleImage.transform.DOPunchScale(originalCircleScale * 0.3f, 0.3f, 5, 1f);
            fillCircleImage.DOFade(0f, 0.3f).OnComplete(() =>
            {
                if (fillCircleImage != null)
                {
                    fillCircleImage.fillAmount = 0f;
                    fillCircleImage.color = Color.white;
                }
            });
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnSystemUpgraded();
    }
}
