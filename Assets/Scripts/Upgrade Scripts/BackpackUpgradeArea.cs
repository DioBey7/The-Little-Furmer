using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BackpackUpgradeArea : MonoBehaviour, IInteractableArea
{
    public TMP_Text costText;
    public TMP_Text capacityInfoText;
    public Image fillCircleImage;

    private float currentFillTime = 0f;
    private bool hasFlashed = false;

    private int cachedMaxLvl = 10;
    private int cachedBaseCost = 1000;
    private float cachedCostMultiplier = 1.5f;
    private float cachedFillDuration = 1.5f;
    private int cachedBaseCap = 5;
    private int cachedCapPerLevel = 3;
    private Transform cachedCostTextTransform;

    private Vector3 originalCircleScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            PlayerAndAiSettings config = LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings;
            cachedMaxLvl = config.backpackMaxLevel;
            cachedBaseCost = config.backpackBaseUpgradeCost;
            cachedCostMultiplier = config.backpackCostMultiplier;
            cachedFillDuration = config.backpackInteractionTimeToUpgrade;
            cachedBaseCap = config.startingBackpackCapacity;
            cachedCapPerLevel = config.capacityIncreasePerBackpackLevel;
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
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.Data.backpackUpgradeLevel : 0;

        if (currentLevel >= cachedMaxLvl)
        {
            ResetPurchase();
            return;
        }

        int currentCost = GetCurrentCost(currentLevel);

        currentFillTime += deltaTime;
        float progress = Mathf.Clamp01(currentFillTime / cachedFillDuration);
        if (fillCircleImage != null) fillCircleImage.fillAmount = progress;

        if (currentFillTime >= cachedFillDuration)
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
                CompleteUpgrade(currentLevel, currentCost, player);
            }
        }
    }

    private int GetCurrentCost(int level)
    {
        return Mathf.RoundToInt(cachedBaseCost * Mathf.Pow(cachedCostMultiplier, level));
    }

    private void UpdateUI()
    {
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.Data.backpackUpgradeLevel : 0;

        if (costText != null)
        {
            costText.DOKill();
            costText.color = Color.white;
            costText.transform.localScale = originalTextScale;

            if (currentLevel >= cachedMaxLvl)
            {
                costText.text = "MAX";
                costText.color = Color.yellow;
            }
            else
            {
                costText.text = "$" + UiManager.FormatMoney(GetCurrentCost(currentLevel));
            }
        }

        if (capacityInfoText != null)
        {
            int currentCapacity = cachedBaseCap + (currentLevel * cachedCapPerLevel);
            capacityInfoText.text = "BAG HOLDS " + UiManager.FormatMoney(currentCapacity);
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

    private void CompleteUpgrade(int currentLevel, int cost, ItemCollecting player)
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Data.backpackUpgradeLevel = currentLevel + 1;
            GameManager.Instance.SaveData();
        }

        hasFlashed = false;
        currentFillTime = 0f;
        UpdateUI();

        if (fillCircleImage != null)
        {
            fillCircleImage.DOKill();
            fillCircleImage.fillAmount = 1f;

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
