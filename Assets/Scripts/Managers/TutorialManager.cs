using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialStep { None, Welcome, Collect, Sell, Produce, UnlockArea, BuyAI, Upgrade, Completed }
    public TutorialStep currentStep = TutorialStep.None;

    [Header("Debug & Calibration")]
    public bool forceTutorial = false;
    public Vector3 arrowRotationOffset = new Vector3(0f, 0f, 0f);
    public float arrowOrbitRadius = 1.35f;
    public float arrowHeightOffset = 0.25f;

    [Header("References")]
    public Transform arrowPointer;
    public GameObject instructionTextObject;

    private Transform playerTransform;
    private ItemCollecting playerInventory;

    private Field.CropArea targetCropArea;
    private StockArea targetStockArea;
    private FactoryArea targetFactoryArea;
    private UnlockArea targetUnlockArea;
    private AiBuyArea targetAiBuyArea;
    private Transform upgradeTarget;

    private Transform currentTarget;
    private Vector3 customTargetPosition;
    private bool useCustomTargetPosition = false;

    private int collectedCount = 0;
    private int targetCollectAmount = 3;
    private int soldCount = 0;
    private int producedCount = 0;

    private Coroutine arrowCoroutine;
    private int currentLevelIndex;
    private bool isInitialized = false;
    private TMP_Text cachedTextComponent;

    private Vector3 originalArrowScale = Vector3.one;
    private Vector3 originalTextScale = Vector3.one;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (instructionTextObject != null)
        {
            cachedTextComponent = instructionTextObject.GetComponentInChildren<TMP_Text>(true);
            originalTextScale = instructionTextObject.transform.localScale;
            if (originalTextScale == Vector3.zero) originalTextScale = Vector3.one;
            instructionTextObject.SetActive(false);
        }

        if (arrowPointer != null)
        {
            originalArrowScale = arrowPointer.localScale;
            if (originalArrowScale == Vector3.zero) originalArrowScale = Vector3.one;
            arrowPointer.gameObject.SetActive(false);
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted -= InitializeAutoTutorial;
            LevelManager.Instance.OnLevelStarted += InitializeAutoTutorial;

            if (LevelManager.Instance.currentLevelInstance != null)
            {
                InitializeAutoTutorial();
            }
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted -= InitializeAutoTutorial;
        }

        if (arrowPointer != null) arrowPointer.DOKill();
        if (instructionTextObject != null) instructionTextObject.transform.DOKill();
    }

    private void InitializeAutoTutorial()
    {
        if (isInitialized) return;

        if (LevelManager.Instance != null)
            currentLevelIndex = LevelManager.Instance.CurrentLevelIndex;

        if (!forceTutorial && PlayerPrefs.GetInt($"TutorialCompleted_Lvl{currentLevelIndex}", 0) == 1)
        {
            CompleteTutorial(false, false);
            return;
        }

        StartCoroutine(DelayedSetupRoutine());
    }

    private IEnumerator DelayedSetupRoutine()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.15f);

        isInitialized = true;
        collectedCount = 0;
        soldCount = 0;
        producedCount = 0;

        AutoFindLevelTargets();
        StartLevelTutorial();
    }

    private void AutoFindLevelTargets()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerInventory = playerObj.GetComponent<ItemCollecting>();
        }
        else
        {
            Character charComp = FindObjectOfType<Character>();
            if (charComp != null)
            {
                playerTransform = charComp.transform;
                playerInventory = charComp.GetComponent<ItemCollecting>();
            }
        }

        Field[] fields = FindObjectsOfType<Field>();
        targetCropArea = null;
        foreach (Field f in fields)
        {
            if (!f.gameObject.activeInHierarchy) continue;
            foreach (var area in f.cropAreas)
            {
                bool isLocked = area.cachedUnlockArea != null && area.cachedUnlockArea.gameObject.activeInHierarchy;
                if (!isLocked && area.dirtPoints.Count > 0)
                {
                    targetCropArea = area;
                    break;
                }
            }
            if (targetCropArea != null) break;
        }

        StockArea[] stocks = FindObjectsOfType<StockArea>();
        targetStockArea = null;
        foreach (StockArea s in stocks)
        {
            if (!s.gameObject.activeInHierarchy) continue;
            if (targetCropArea != null && s.isTypeCached && s.AcceptedItemType == targetCropArea.cachedItemType)
            {
                targetStockArea = s;
                break;
            }
        }
        if (targetStockArea == null && stocks.Length > 0) targetStockArea = stocks[0];

        FactoryArea[] factories = FindObjectsOfType<FactoryArea>();
        targetFactoryArea = null;
        foreach (FactoryArea fac in factories)
        {
            if (fac.gameObject.activeInHierarchy)
            {
                targetFactoryArea = fac;
                break;
            }
        }

        UnlockArea[] unlocks = FindObjectsOfType<UnlockArea>();
        targetUnlockArea = null;
        float minUnlockDist = Mathf.Infinity;
        foreach (UnlockArea u in unlocks)
        {
            if (u.gameObject.activeInHierarchy)
            {
                float d = playerTransform != null ? Vector3.Distance(playerTransform.position, u.transform.position) : 0f;
                if (d < minUnlockDist)
                {
                    minUnlockDist = d;
                    targetUnlockArea = u;
                }
            }
        }

        AiBuyArea[] aiBuys = FindObjectsOfType<AiBuyArea>();
        targetAiBuyArea = null;
        foreach (AiBuyArea a in aiBuys)
        {
            if (a.gameObject.activeInHierarchy)
            {
                targetAiBuyArea = a;
                break;
            }
        }

        upgradeTarget = null;
        BackpackUpgradeArea bagUpgrade = FindObjectOfType<BackpackUpgradeArea>();
        if (bagUpgrade != null && bagUpgrade.gameObject.activeInHierarchy)
        {
            upgradeTarget = GetInteractableCenter(bagUpgrade.transform);
        }
        else
        {
            StockUpgradeArea stockUpg = FindObjectOfType<StockUpgradeArea>();
            if (stockUpg != null && stockUpg.gameObject.activeInHierarchy)
                upgradeTarget = GetInteractableCenter(stockUpg.transform);
            else
            {
                FieldUpgradeArea fieldUpg = FindObjectOfType<FieldUpgradeArea>();
                if (fieldUpg != null && fieldUpg.gameObject.activeInHierarchy)
                    upgradeTarget = GetInteractableCenter(fieldUpg.transform);
            }
        }
    }

    private Vector3 GetCropAreaCenter(Field.CropArea area)
    {
        if (area == null || area.dirtPoints == null || area.dirtPoints.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int activeCount = 0;
        foreach (Transform t in area.dirtPoints)
        {
            if (t != null && t.gameObject.activeInHierarchy)
            {
                sum += t.position;
                activeCount++;
            }
        }
        if (activeCount > 0) return sum / activeCount;
        return area.areaRoot != null ? area.areaRoot.position : area.dirtPoints[0].position;
    }

    private Transform GetStockTargetTransform(StockArea stock)
    {
        if (stock == null) return null;
        if (stock.fillPoint != null) return stock.fillPoint;
        StockPointReference refComp = stock.GetComponentInChildren<StockPointReference>();
        if (refComp != null) return refComp.transform;
        return stock.transform;
    }

    private Transform GetFactoryTargetTransform(FactoryArea factory)
    {
        if (factory == null) return null;
        if (factory.inputPoint != null) return factory.inputPoint;
        FactoryTrigger trig = factory.GetComponentInChildren<FactoryTrigger>();
        if (trig != null) return trig.transform;
        return factory.transform;
    }

    private Transform GetInteractableCenter(Transform root)
    {
        if (root == null) return null;
        Collider col = root.GetComponentInChildren<Collider>();
        if (col != null) return col.transform;
        return root;
    }

    private void StartLevelTutorial()
    {
        if (currentLevelIndex == 0)
        {
            if (targetCropArea != null)
            {
                StartCoroutine(WelcomeSequenceRoutine());
            }
            else CompleteTutorial(false, false);
        }
        else if (currentLevelIndex == 1)
        {
            if (targetUnlockArea != null)
            {
                TransitionToStep(TutorialStep.UnlockArea, GetInteractableCenter(targetUnlockArea.transform), "STEP ON THE ZONE TO\nUNLOCK THE NEW AREA!");
            }
            else if (targetAiBuyArea != null)
            {
                TransitionToStep(TutorialStep.BuyAI, GetInteractableCenter(targetAiBuyArea.transform), "HIRE AN AI HELPER TO\nAUTOMATE YOUR FARM!");
            }
            else CompleteTutorial(false, false);
        }
        else if (currentLevelIndex == 2)
        {
            if (upgradeTarget != null)
            {
                TransitionToStep(TutorialStep.Upgrade, upgradeTarget, "SELL CROPS TO EARN CASH,\nTHEN UPGRADE YOUR CAPACITY!");
            }
            else CompleteTutorial(false, false);
        }
        else
        {
            CompleteTutorial(false, false);
        }
    }

    private IEnumerator WelcomeSequenceRoutine()
    {
        currentStep = TutorialStep.Welcome;
        SetInstructionText("WELCOME TO THE LITTLE FURMER!\nLET'S BUILD YOUR FARM EMPIRE!");

        yield return new WaitForSeconds(2.6f);

        Vector3 cropCenter = GetCropAreaCenter(targetCropArea);
        string cropName = targetCropArea != null ? targetCropArea.cachedItemType.ToString().ToUpper() + "S" : "CROPS";

        TransitionToCustomPosStep(TutorialStep.Collect, cropCenter, $"WALK OVER THE FIELD TO\nHARVEST {targetCollectAmount} {cropName}! (0/{targetCollectAmount})");
    }

    private void TransitionToStep(TutorialStep nextStep, Transform targetTransform, string instruction)
    {
        currentStep = nextStep;
        currentTarget = targetTransform;
        useCustomTargetPosition = false;

        SetInstructionText(instruction);
        ActivateArrow();
    }

    private void TransitionToCustomPosStep(TutorialStep nextStep, Vector3 worldPos, string instruction)
    {
        currentStep = nextStep;
        currentTarget = null;
        customTargetPosition = worldPos;
        useCustomTargetPosition = true;

        SetInstructionText(instruction);
        ActivateArrow();
    }

    private void ActivateArrow()
    {
        if (arrowPointer == null) return;

        arrowPointer.gameObject.SetActive(true);
        arrowPointer.DOKill();
        arrowPointer.localScale = originalArrowScale;
        arrowPointer.DOScale(originalArrowScale * 1.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        if (arrowCoroutine != null) StopCoroutine(arrowCoroutine);
        arrowCoroutine = StartCoroutine(ArrowPointerRoutine());
    }

    private IEnumerator ArrowPointerRoutine()
    {
        while (currentStep != TutorialStep.Completed && arrowPointer != null)
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            Vector3 targetPos = GetActiveTargetWorldPosition();

            if (playerTransform != null && targetPos != Vector3.zero)
            {
                Vector3 direction = targetPos - playerTransform.position;
                direction.y = 0f;
                float distance = direction.magnitude;

                if (distance < 1.15f)
                {
                    arrowPointer.position = Vector3.Lerp(arrowPointer.position, targetPos + Vector3.up * 1.2f, Time.deltaTime * 12f);
                }
                else
                {
                    Vector3 normDir = direction.normalized;
                    Vector3 orbitWorldPos = playerTransform.position + (normDir * arrowOrbitRadius) + (Vector3.up * arrowHeightOffset);
                    arrowPointer.position = orbitWorldPos;

                    Quaternion lookRot = Quaternion.LookRotation(normDir, Vector3.up) * Quaternion.Euler(arrowRotationOffset);
                    arrowPointer.rotation = Quaternion.Slerp(arrowPointer.rotation, lookRot, Time.deltaTime * 20f);
                }
            }

            yield return null;
        }
    }

    private Vector3 GetActiveTargetWorldPosition()
    {
        if (currentStep == TutorialStep.Produce && targetFactoryArea != null)
        {
            bool playerHasIngredient = true;
            if (playerInventory != null && targetFactoryArea.requiredIngredients.Count > 0)
            {
                ItemType neededType = targetFactoryArea.requiredIngredients[0].itemType;
                playerHasIngredient = playerInventory.HasItem(neededType);
            }

            if (!playerHasIngredient && targetCropArea != null)
            {
                return GetCropAreaCenter(targetCropArea);
            }

            Transform facInput = GetFactoryTargetTransform(targetFactoryArea);
            return facInput != null ? facInput.position : targetFactoryArea.transform.position;
        }

        if (useCustomTargetPosition) return customTargetPosition;

        if (currentTarget != null)
        {
            if (currentTarget.TryGetComponent(out Collider col))
                return col.bounds.center;
            return currentTarget.position;
        }

        return Vector3.zero;
    }

    public void OnItemCollected()
    {
        if (currentStep == TutorialStep.Collect)
        {
            collectedCount++;
            int clamped = Mathf.Min(collectedCount, targetCollectAmount);
            string cropName = targetCropArea != null ? targetCropArea.cachedItemType.ToString().ToUpper() + "S" : "CROPS";

            UpdateInstructionTextSubtle($"WALK OVER THE FIELD TO\nHARVEST {targetCollectAmount} {cropName}! ({clamped}/{targetCollectAmount})");

            if (collectedCount >= targetCollectAmount)
            {
                Transform stockSpot = GetStockTargetTransform(targetStockArea);
                TransitionToStep(TutorialStep.Sell, stockSpot, "STAND ON THE CIRCLE TO\nDROP CROPS AT THE STAND & EARN CASH!");
            }
        }
        else if (currentStep == TutorialStep.Produce)
        {
            UpdateInstructionTextSubtle("GREAT! NOW STAND ON THE FACTORY\nINPUT CIRCLE TO PRODUCE GOODS!");
        }
    }

    public void OnItemSold()
    {
        if (currentStep != TutorialStep.Sell) return;
        soldCount++;

        if (soldCount >= 1)
        {
            if (targetFactoryArea != null)
            {
                Transform facSpot = GetFactoryTargetTransform(targetFactoryArea);
                TransitionToStep(TutorialStep.Produce, facSpot, "COLLECT CROPS & DELIVER THEM\nTO THE FACTORY TO PRODUCE!");
            }
            else
            {
                CompleteTutorial(true, true);
            }
        }
    }

    public void OnItemProduced()
    {
        if (currentStep != TutorialStep.Produce) return;
        producedCount++;

        if (producedCount >= 1)
        {
            CompleteTutorial(true, true);
        }
    }

    public void OnAreaUnlocked()
    {
        if (currentStep != TutorialStep.UnlockArea) return;

        if (targetAiBuyArea == null)
        {
            targetAiBuyArea = FindObjectOfType<AiBuyArea>();
        }

        if (targetAiBuyArea != null && targetAiBuyArea.gameObject.activeInHierarchy)
        {
            TransitionToStep(TutorialStep.BuyAI, GetInteractableCenter(targetAiBuyArea.transform), "AWESOME! NOW HIRE AN AI WORKER\nTO AUTOMATE HARVESTING!");
        }
        else
        {
            CompleteTutorial(true, true);
        }
    }

    public void OnAiPurchased()
    {
        if (currentStep != TutorialStep.BuyAI) return;
        CompleteTutorial(true, true);
    }

    public void OnSystemUpgraded()
    {
        if (currentStep != TutorialStep.Upgrade) return;
        CompleteTutorial(true, true);
    }

    private void CompleteTutorial(bool playFx, bool showCompletionMessage)
    {
        StartCoroutine(CompleteTutorialRoutine(playFx, showCompletionMessage));
    }

    private IEnumerator CompleteTutorialRoutine(bool playFx, bool showCompletionMessage)
    {
        currentStep = TutorialStep.Completed;
        isInitialized = false;

        if (arrowCoroutine != null) StopCoroutine(arrowCoroutine);

        if (arrowPointer != null)
        {
            arrowPointer.DOKill();
            arrowPointer.gameObject.SetActive(false);
        }

        PlayerPrefs.SetInt($"TutorialCompleted_Lvl{currentLevelIndex}", 1);
        PlayerPrefs.Save();

        if (playFx && SoundManager.Instance != null && SoundManager.Instance.levelUpClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.levelUpClip);
        }

        if (showCompletionMessage)
        {
            SetInstructionText("TUTORIAL COMPLETED!\nCOMPLETE THE OBJECTIVES ABOVE!");
            yield return new WaitForSeconds(2.8f);
        }

        if (instructionTextObject != null)
        {
            instructionTextObject.transform.DOKill();
            instructionTextObject.transform.DOScale(Vector3.zero, 0.35f).SetEase(Ease.InBack).OnComplete(() =>
            {
                instructionTextObject.SetActive(false);
            });
        }
    }

    /// <summary>
    /// Hides the arrow and banner at once when the level ends, without marking the tutorial as done.
    /// Also lets the next level start its own tutorial (InitializeAutoTutorial skips while initialized).
    /// </summary>
    public void HideForLevelComplete()
    {
        StopAllCoroutines();
        arrowCoroutine = null;
        currentStep = TutorialStep.None;
        isInitialized = false;

        if (arrowPointer != null)
        {
            arrowPointer.DOKill();
            arrowPointer.gameObject.SetActive(false);
        }

        if (instructionTextObject != null)
        {
            instructionTextObject.transform.DOKill();
            instructionTextObject.transform.localScale = originalTextScale;
            instructionTextObject.SetActive(false);
        }
    }

    private void SetInstructionText(string text)
    {
        if (instructionTextObject == null) return;

        instructionTextObject.SetActive(true);

        if (cachedTextComponent != null)
        {
            cachedTextComponent.text = text;
        }

        instructionTextObject.transform.DOKill();
        instructionTextObject.transform.localScale = Vector3.zero;

        Sequence textSeq = DOTween.Sequence();
        textSeq.Append(instructionTextObject.transform.DOScale(originalTextScale * 1.15f, 0.35f).SetEase(Ease.OutBack));
        textSeq.Append(instructionTextObject.transform.DOScale(originalTextScale, 0.15f).SetEase(Ease.InSine));

        instructionTextObject.transform.DOScale(originalTextScale * 1.05f, 0.9f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetDelay(0.5f);
    }

    private void UpdateInstructionTextSubtle(string text)
    {
        if (instructionTextObject == null || cachedTextComponent == null) return;
        cachedTextComponent.text = text;
        instructionTextObject.transform.DOKill();
        instructionTextObject.transform.localScale = originalTextScale;
        instructionTextObject.transform.DOPunchScale(originalTextScale * 0.12f, 0.25f, 5, 1f);
    }
}
