using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialStep { None, Welcome, Collect, Sell, Produce, UnlockArea, BuyAI, Upgrade, Completed }
    public TutorialStep currentStep = TutorialStep.None;

    [Header("Debug")]
    public bool forceTutorial = false;

    public Transform arrowPointer;
    public GameObject instructionTextObject;

    private GameObject fieldObject, stockObject, factoryObject, unlockAreaObject, aiBuyObject, upgradeObject;
    private Transform fieldTarget, stockTarget, factoryTarget, unlockAreaTarget, aiBuyTarget, upgradeTarget;

    private Transform currentTarget;

    private int collectedCount = 0;
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
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted -= InitializeAutoTutorial;
            LevelManager.Instance.OnLevelStarted += InitializeAutoTutorial;

            if (LevelManager.Instance.currentLevelInstance != null)
            {
                InitializeAutoTutorial();
            }
        }

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

        currentLevelIndex = LevelManager.Instance.CurrentLevelIndex;

        if (!forceTutorial && PlayerPrefs.GetInt($"TutorialCompleted_Lvl{currentLevelIndex}", 0) == 1)
        {
            CompleteTutorial(false, false);
            return;
        }

        isInitialized = true;
        AutoFindLevelTargets();
        StartLevelTutorial();
    }

    private void AutoFindLevelTargets()
    {
        Field field = FindObjectOfType<Field>();
        if (field != null) { fieldObject = field.gameObject; fieldTarget = field.transform; }

        StockArea stock = FindObjectOfType<StockArea>();
        if (stock != null) { stockObject = stock.gameObject; stockTarget = stock.transform; }

        FactoryArea factory = FindObjectOfType<FactoryArea>();
        if (factory != null) { factoryObject = factory.gameObject; factoryTarget = factory.transform; }

        UnlockArea unlock = FindObjectOfType<UnlockArea>();
        if (unlock != null) { unlockAreaObject = unlock.gameObject; unlockAreaTarget = unlock.transform; }

        AiBuyArea aiBuy = FindObjectOfType<AiBuyArea>();
        if (aiBuy != null) { aiBuyObject = aiBuy.gameObject; aiBuyTarget = aiBuy.transform; }

        BackpackUpgradeArea bagUpgrade = FindObjectOfType<BackpackUpgradeArea>();
        if (bagUpgrade != null)
        {
            upgradeObject = bagUpgrade.gameObject;
            upgradeTarget = bagUpgrade.transform;
        }
        else
        {
            AiUpgradeArea aiUpgrade = FindObjectOfType<AiUpgradeArea>();
            if (aiUpgrade != null)
            {
                upgradeObject = aiUpgrade.gameObject;
                upgradeTarget = aiUpgrade.transform;
            }
        }
    }

    private void StartLevelTutorial()
    {
        if (currentLevelIndex == 0)
        {
            if (fieldTarget != null)
            {
                StartCoroutine(WelcomeSequenceRoutine());
            }
            else CompleteTutorial(false, false);
        }
        else if (currentLevelIndex == 1)
        {
            if (unlockAreaTarget != null) TransitionToStep(TutorialStep.UnlockArea, unlockAreaTarget, "EXPLORE AND UNLOCK\nTHE NEW AREA!");
            else CompleteTutorial(false, false);
        }
        else if (currentLevelIndex == 2)
        {
            if (upgradeTarget != null) TransitionToStep(TutorialStep.Upgrade, upgradeTarget, "EARN CASH BY SELLING CROPS,\nTHEN UPGRADE YOUR CAPACITY!");
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
        SetInstructionText("WELCOME TO YOUR FARM!\nLET'S GET STARTED!");

        yield return new WaitForSeconds(3.5f);

        TransitionToStep(TutorialStep.Collect, fieldTarget, "AT FIRST\nHARVEST 3 CROPS!");
    }

    private void TransitionToStep(TutorialStep nextStep, Transform originalTarget, string instruction)
    {
        currentStep = nextStep;
        currentTarget = originalTarget;

        SetInstructionText(instruction);

        if (currentTarget != null)
        {
            if (arrowPointer != null)
            {
                arrowPointer.gameObject.SetActive(true);

                arrowPointer.DOKill();
                arrowPointer.localScale = originalArrowScale;
                arrowPointer.DOScale(originalArrowScale * 1.3f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                if (arrowCoroutine != null) StopCoroutine(arrowCoroutine);
                arrowCoroutine = StartCoroutine(ArrowPointerRoutine());
            }
        }
    }

    private IEnumerator ArrowPointerRoutine()
    {
        while (currentStep != TutorialStep.Completed && currentTarget != null && arrowPointer != null)
        {
            Vector3 direction = currentTarget.position - arrowPointer.position;
            direction.y = 0;

            float distance = direction.magnitude;

            if (currentStep == TutorialStep.UnlockArea)
            {
                if (distance < 3.5f)
                {
                    HandleDiscoveryStep();
                }
            }

            if (distance > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                arrowPointer.rotation = Quaternion.Slerp(arrowPointer.rotation, targetRotation, Time.deltaTime * 15f);
            }
            yield return null;
        }
    }

    private void HandleDiscoveryStep()
    {
        if (currentStep == TutorialStep.UnlockArea)
        {
            if (aiBuyTarget != null)
                TransitionToStep(TutorialStep.BuyAI, aiBuyTarget, "HIRE AN AI WORKER\nTO HELP YOU OUT!");
            else
                CompleteTutorial(true, true);
        }
    }

    public void OnItemCollected()
    {
        if (currentStep != TutorialStep.Collect) return;
        collectedCount++;

        if (collectedCount >= 3)
        {
            TransitionToStep(TutorialStep.Sell, stockTarget, "BRING YOUR CROPS TO\nITS STOCK AREA!");
        }
    }

    public void OnItemSold()
    {
        if (currentStep != TutorialStep.Sell) return;
        soldCount++;

        if (soldCount >= 1)
        {
            TransitionToStep(TutorialStep.Produce, factoryTarget, "HARVEST AND PUT CROPS IN THE SHOP\nTO PRODUCE GOODS!");
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
        HandleDiscoveryStep();
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
            SetInstructionText("LEVEL TUTORIAL COMPLETED!\nGREAT JOB!");
            yield return new WaitForSeconds(2.5f);
        }

        if (instructionTextObject != null)
        {
            instructionTextObject.transform.DOKill();
            instructionTextObject.transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).OnComplete(() =>
            {
                instructionTextObject.SetActive(false);
            });
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
        textSeq.Append(instructionTextObject.transform.DOScale(originalTextScale * 1.15f, 0.4f).SetEase(Ease.OutElastic, 1.5f, 1f));
        textSeq.Append(instructionTextObject.transform.DOScale(originalTextScale, 0.15f).SetEase(Ease.InSine));

        instructionTextObject.transform.DOScale(originalTextScale * 1.05f, 1f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetDelay(0.55f);
    }
}
