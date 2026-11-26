using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class StockUpgradeArea : MonoBehaviour, IInteractableArea
{
    public TMP_Text costText;
    public TMP_Text infoText;
    public Image fillCircleImage;

    private float currentFillTime = 0f;
    private bool hasFlashed = false;
    private StockArea targetStockArea;
    private Vector3 originalCircleScale = Vector3.one;

    private int GetBaseCost() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.baseUpgradeCost : 1000;
    private float GetCostMultiplier() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.upgradeCostMultiplier : 1.5f;
    private int GetMaxLevel() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.maximumUpgradeLevel : 5;
    private float GetFillDuration() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.interactionTimeToUpgrade : 1.0f;

    private void Awake()
    {
        targetStockArea = GetComponentInParent<StockArea>();

        if (fillCircleImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.type == Image.Type.Filled)
                {
                    fillCircleImage = img;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        if (fillCircleImage != null)
        {
            fillCircleImage.type = Image.Type.Filled;
            fillCircleImage.fillMethod = Image.FillMethod.Radial360;
            originalCircleScale = fillCircleImage.transform.localScale;
            if (originalCircleScale == Vector3.zero) originalCircleScale = Vector3.one;
            fillCircleImage.fillAmount = 0f;
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

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (targetStockArea == null) return;

        int currentLevel = GetCurrentLevel();
        int maxLvl = GetMaxLevel();

        if (currentLevel >= maxLvl)
        {
            ResetPurchase();
            return;
        }

        int currentCost = GetCurrentCost(currentLevel);

        currentFillTime += deltaTime;
        float dur = GetFillDuration();
        float progress = Mathf.Clamp01(currentFillTime / dur);

        if (fillCircleImage != null) fillCircleImage.fillAmount = progress;

        if (currentFillTime >= dur)
        {
            if (UiManager.Instance == null || UiManager.Instance.CurrentCash < currentCost)
            {
                if (!hasFlashed && costText != null && !DOTween.IsTweening(costText.transform))
                {
                    hasFlashed = true;
                    costText.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.3f, 5, 1f);
                    costText.color = Color.red;
                    DOVirtual.DelayedCall(0.8f, () => UpdateUI());
                }
                ResetPurchase();
            }
            else
            {
                CompleteUpgrade(currentLevel, currentCost);
            }
        }
    }

    private int GetCurrentLevel()
    {
        if (GameManager.Instance == null || targetStockArea == null) return 0;
        var data = GameManager.Instance.Data.stockUpgrades.Find(x => x.stockID == targetStockArea.stockID);
        return data != null ? data.level : 0;
    }

    private int GetCurrentCost(int level)
    {
        return Mathf.RoundToInt(GetBaseCost() * Mathf.Pow(GetCostMultiplier(), level));
    }

    private void UpdateUI()
    {
        int currentLevel = GetCurrentLevel();
        int maxLvl = GetMaxLevel();

        if (infoText != null && targetStockArea != null)
        {
            int capacity = targetStockArea.GetActiveCapacity();
            infoText.text = $"STOCK LV.{currentLevel + 1}\nHOLDS {capacity}";
        }

        if (costText != null)
        {
            costText.DOKill();
            costText.color = Color.white;

            if (currentLevel >= maxLvl)
            {
                costText.text = "MAX";
                costText.color = Color.yellow;
            }
            else
            {
                costText.text = "$" + UiManager.FormatMoney(GetCurrentCost(currentLevel));
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
            UiManager.Instance.AddNotification("-$" + UiManager.FormatMoney(cost), Color.red);
            UiManager.Instance.AddNotification("UPGRADED!", Color.yellow);
        }

        if (SoundManager.Instance != null && SoundManager.Instance.upgradeClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.upgradeClip);
        }

        if (FxManager.Instance != null)
        {
            FxManager.Instance.PlayFx("LevelUp", transform.position + Vector3.up);
        }

        if (GameManager.Instance != null && targetStockArea != null)
        {
            var data = GameManager.Instance.Data.stockUpgrades.Find(x => x.stockID == targetStockArea.stockID);
            if (data != null)
            {
                data.level = currentLevel + 1;
            }
            else
            {
                GameManager.Instance.Data.stockUpgrades.Add(new StockSaveData { stockID = targetStockArea.stockID, level = currentLevel + 1 });
            }
            GameManager.Instance.SaveData();
        }

        if (targetStockArea != null)
        {
            targetStockArea.RefreshVisuals();
        }

        hasFlashed = false;
        currentFillTime = 0f;
        UpdateUI();

        if (fillCircleImage != null)
        {
            fillCircleImage.DOKill();
            fillCircleImage.fillAmount = 1f;
            fillCircleImage.color = Color.white;

            fillCircleImage.transform.DOPunchScale(originalCircleScale * 0.2f, 0.3f, 5, 1f);
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
