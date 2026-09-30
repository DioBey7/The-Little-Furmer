using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class AiUpgradeArea : MonoBehaviour, IInteractableArea
{
    public TMP_Text costText;
    public TMP_Text capacityInfoText;
    public Image fillCircleImage;

    private float currentFillTime = 0f;
    private bool hasFlashed = false;
    private Transform cachedCostTextTransform;

    private Vector3 originalCircleScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    private void Start()
    {
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
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.Data.aiCapacityUpgradeLevel : 0;
        int maxLvl = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.aiMaxLevel : 10;

        if (currentLevel >= maxLvl)
        {
            ResetPurchase();
            return;
        }

        int currentCost = GetCurrentCost(currentLevel);
        float dur = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.aiInteractionTimeToUpgrade : 1.5f;

        currentFillTime += deltaTime;
        float progress = Mathf.Clamp01(currentFillTime / dur);
        if (fillCircleImage != null) fillCircleImage.fillAmount = progress;

        if (currentFillTime >= dur)
        {
            if (UiManager.Instance == null || UiManager.Instance.CurrentCash < currentCost)
            {
                if (!hasFlashed && costText != null && cachedCostTextTransform != null && !DOTween.IsTweening(cachedCostTextTransform))
                {
                    hasFlashed = true;
                    cachedCostTextTransform.DOPunchScale(originalTextScale * 0.3f, 0.3f, 5, 1f);
                    costText.color = Color.red;
                    DOVirtual.DelayedCall(0.5f, () => UpdateUI());
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
        int baseCost = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.aiBaseUpgradeCost : 2000;
        float mult = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.aiCostMultiplier : 1.5f;
        return Mathf.RoundToInt(baseCost * Mathf.Pow(mult, level));
    }

    private void UpdateUI()
    {
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.Data.aiCapacityUpgradeLevel : 0;
        int maxLvl = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.aiMaxLevel : 10;

        if (costText != null)
        {
            costText.DOKill();
            costText.color = Color.white;
            costText.transform.localScale = originalTextScale;

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

        if (capacityInfoText != null)
        {
            int baseCap = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.startingAiCapacity : 3;
            int capPerLevel = LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.capacityIncreasePerAiLevel : 1;

            int displayCapacity = baseCap + (currentLevel * capPerLevel);
            capacityInfoText.text = "AI BAG CAPACITY: " + UiManager.FormatMoney(displayCapacity);
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
            UiManager.Instance.AddNotification("AI UPGRADED!", Color.yellow);
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
            GameManager.Instance.Data.aiCapacityUpgradeLevel = currentLevel + 1;
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
