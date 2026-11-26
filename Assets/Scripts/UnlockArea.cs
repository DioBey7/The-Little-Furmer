using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnlockArea : MonoBehaviour, IInteractableArea
{
    public enum AreaType { Field, Shop, Stock, General }

    public int areaID = 1;
    public AreaType areaType = AreaType.General;
    public string customAreaName = "AREA NAME";
    public GameObject[] objectsToUnlock;

    public TMP_Text costText;
    public TMP_Text areaNameText;
    public Image fillCircleImage;
    public Transform lockIcon;

    private int totalCost = 500;
    private int remainingCost;
    private bool isUnlocked = false;
    private bool initiallyUnlockedFromData = false;

    private float currentFillTime = 0f;
    private float failTimer = 0f;
    private bool hasFlashed = false;

    private bool isFillingComplete = false;
    private float deductionTimer = 0f;

    private float cachedFillDuration = 1.0f;
    private float cachedDeductionInterval = 0.05f;
    private int cachedDeductionAmount = 500;
    private Transform cachedCostTextTransform;
    private Vector3 originalCostTextScale = Vector3.one;

    private void Awake()
    {
        if (TryGetComponent(out Collider col)) col.isTrigger = true;

        if (!TryGetComponent(out Rigidbody rb)) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (costText != null)
        {
            cachedCostTextTransform = costText.transform;
            originalCostTextScale = cachedCostTextTransform.localScale;
        }

        ToggleParentLogic(false);

        for (int i = 0; i < objectsToUnlock.Length; i++)
        {
            if (objectsToUnlock[i] != null) objectsToUnlock[i].SetActive(false);
        }
    }

    private void Start()
    {
        InitializeData();

        bool alreadyUnlockedInSave = false;
        if (GameManager.Instance != null)
        {
            List<int> unlockedList = GameManager.Instance.Data.unlockedAreas;
            for (int i = 0; i < unlockedList.Count; i++)
            {
                if (unlockedList[i] == areaID)
                {
                    alreadyUnlockedInSave = true;
                    break;
                }
            }
        }

        if (initiallyUnlockedFromData || alreadyUnlockedInSave)
        {
            UnlockInstant();
        }
        else
        {
            gameObject.SetActive(true);
            remainingCost = totalCost;

            if (GameManager.Instance != null)
            {
                List<AreaProgressData> progressList = GameManager.Instance.Data.areaProgress;
                for (int i = 0; i < progressList.Count; i++)
                {
                    if (progressList[i].areaID == this.areaID)
                    {
                        remainingCost = progressList[i].remainingCost;
                        break;
                    }
                }
            }

            UpdateUI();
        }

        if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;
    }

    private void InitializeData()
    {
        totalCost = 500;

        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            LevelConfig config = LevelManager.Instance.CurrentLevelConfig;

            cachedFillDuration = config.unlockMechanics.circleFillTime;
            cachedDeductionInterval = config.unlockMechanics.cashDeductionInterval;
            cachedDeductionAmount = config.unlockMechanics.cashDeductedPerInterval;

            List<UnlockableAreaConfig> targetList = null;

            switch (areaType)
            {
                case AreaType.Field: targetList = config.fieldAreasToUnlock; break;
                case AreaType.Shop: targetList = config.factoryAreasToUnlock; break;
                case AreaType.Stock: targetList = config.stockAreasToUnlock; break;
                case AreaType.General: targetList = config.generalAreasToUnlock; break;
            }

            if (targetList != null)
            {
                for (int i = 0; i < targetList.Count; i++)
                {
                    if (targetList[i].uniqueAreaID == this.areaID)
                    {
                        totalCost = targetList[i].requiredCashToUnlock;
                        initiallyUnlockedFromData = targetList[i].isUnlockedByStart;
                        break;
                    }
                }
            }
        }

        if (areaNameText != null)
        {
            string typePrefix = areaType.ToString();
            string finalName = string.IsNullOrEmpty(customAreaName) ? "AREA" : customAreaName;
            areaNameText.text = $"<size=80%><b>{finalName}</b></size>\n<size=120%><b><color=#FFEB3B>{typePrefix}</color></b></size>";
            areaNameText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ToggleParentLogic(bool state)
    {
        Transform parentTransform = transform.parent;
        if (parentTransform != null)
        {
            if (parentTransform.TryGetComponent(out Field field)) field.enabled = state;
            if (parentTransform.TryGetComponent(out FactoryArea factory)) factory.enabled = state;
            if (parentTransform.TryGetComponent(out StockArea stock)) stock.enabled = state;
            if (parentTransform.TryGetComponent(out MarketArea market)) market.enabled = state;
        }
    }

    public void OnPlayerEnter(ItemCollecting player)
    {
        if (player == null) return;
        failTimer = 0f;
        hasFlashed = false;
        UpdateUI();
    }

    public void OnPlayerExit(ItemCollecting player)
    {
        if (player == null) return;
        failTimer = 0f;
        hasFlashed = false;
        ResetFill();
        SavePartialProgress();
        UpdateUI();
    }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (player == null || isUnlocked || remainingCost <= 0) return;

        if (!isFillingComplete)
        {
            currentFillTime += deltaTime;
            float progress = Mathf.Clamp01(currentFillTime / cachedFillDuration);

            if (fillCircleImage != null)
            {
                fillCircleImage.fillAmount = progress;
            }

            if (currentFillTime >= cachedFillDuration)
            {
                isFillingComplete = true;
                deductionTimer = cachedDeductionInterval;
            }
        }
        else
        {
            int availableCash = UiManager.Instance != null ? UiManager.Instance.CurrentCash : 0;

            if (availableCash <= 0)
            {
                failTimer += deltaTime;
                if (failTimer > 0.1f && !hasFlashed && costText != null && cachedCostTextTransform != null && !DOTween.IsTweening(cachedCostTextTransform))
                {
                    hasFlashed = true;

                    cachedCostTextTransform.localScale = originalCostTextScale;
                    cachedCostTextTransform.DOPunchScale(originalCostTextScale * 0.3f, 0.3f, 5, 1f);

                    costText.color = Color.red;
                    costText.text = "<size=65%>NOT ENOUGH CASH</size>";

                    DOVirtual.DelayedCall(1.0f, () =>
                    {
                        if (costText != null)
                        {
                            hasFlashed = false;
                            costText.color = Color.white;
                            cachedCostTextTransform.localScale = originalCostTextScale;
                            UpdateUI();
                        }
                    });
                }
                return;
            }

            failTimer = 0f;
            hasFlashed = false;

            deductionTimer += deltaTime;
            if (deductionTimer >= cachedDeductionInterval)
            {
                deductionTimer = 0f;

                // Pay at least 1/25 of the total per tick so even big areas unlock in a couple of seconds.
                int perTick = Mathf.Max(cachedDeductionAmount, Mathf.CeilToInt(totalCost / 25f));
                int deduction = Mathf.Min(remainingCost, Mathf.Min(perTick, availableCash));

                if (UiManager.Instance != null) UiManager.Instance.SpendCash(deduction);
                remainingCost -= deduction;

                if (lockIcon != null && !DOTween.IsTweening(lockIcon))
                {
                    lockIcon.DOPunchScale(new Vector3(0.15f, 0.15f, 0.15f), 0.1f);
                }

                UpdateUI();

                if (remainingCost <= 0)
                {
                    UnlockWithAnimation();
                }
            }
        }
    }

    private void UpdateUI()
    {
        if (costText != null && !hasFlashed)
        {
            costText.color = Color.white;
            costText.text = "$" + UiManager.FormatMoney(remainingCost);
        }
    }

    private void ResetFill()
    {
        isFillingComplete = false;
        currentFillTime = 0f;
        deductionTimer = 0f;

        if (fillCircleImage != null)
        {
            fillCircleImage.fillAmount = 0f;
        }
    }

    private void SavePartialProgress()
    {
        if (GameManager.Instance == null || remainingCost >= totalCost || isUnlocked) return;

        List<AreaProgressData> progressList = GameManager.Instance.Data.areaProgress;
        AreaProgressData existingProgress = null;

        for (int i = 0; i < progressList.Count; i++)
        {
            if (progressList[i].areaID == this.areaID)
            {
                existingProgress = progressList[i];
                break;
            }
        }

        if (existingProgress != null)
        {
            existingProgress.remainingCost = this.remainingCost;
        }
        else
        {
            AreaProgressData newData = new AreaProgressData();
            newData.areaID = this.areaID;
            newData.remainingCost = this.remainingCost;
            progressList.Add(newData);
        }

        GameManager.Instance.SaveData();
    }

    private void UnlockInstant()
    {
        isUnlocked = true;
        ToggleParentLogic(true);
        for (int i = 0; i < objectsToUnlock.Length; i++)
        {
            if (objectsToUnlock[i] != null) objectsToUnlock[i].SetActive(true);
        }
        gameObject.SetActive(false);
    }

    private void UnlockWithAnimation()
    {
        isUnlocked = true;
        ToggleParentLogic(true);

        if (SoundManager.Instance != null && SoundManager.Instance.unlockAreaClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.unlockAreaClip);
        }

        if (GameManager.Instance != null)
        {
            List<AreaProgressData> progressList = GameManager.Instance.Data.areaProgress;
            for (int i = 0; i < progressList.Count; i++)
            {
                if (progressList[i].areaID == this.areaID)
                {
                    progressList.RemoveAt(i);
                    break;
                }
            }

            if (!initiallyUnlockedFromData && !GameManager.Instance.Data.unlockedAreas.Contains(areaID))
            {
                GameManager.Instance.Data.unlockedAreas.Add(areaID);
            }

            GameManager.Instance.SaveData();
        }

        if (FxManager.Instance != null)
        {
            FxManager.Instance.PlayFx("UnlockArea", transform.position);
        }

        for (int i = 0; i < objectsToUnlock.Length; i++)
        {
            GameObject obj = objectsToUnlock[i];
            if (obj != null)
            {
                obj.SetActive(true);

                Vector3 targetScale = obj.transform.localScale;
                if (targetScale == Vector3.zero) targetScale = Vector3.one;

                obj.transform.localScale = Vector3.zero;
                obj.transform.DOScale(targetScale, 0.6f).SetEase(Ease.OutBack);
            }
        }

        gameObject.SetActive(false);

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnAreaUnlocked();
    }
}
