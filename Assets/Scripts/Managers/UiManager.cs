using System.Collections.Generic;
using CitrioN.Common;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiManager : MonoBehaviour
{
    public static UiManager Instance { get; private set; }

    [System.Serializable]
    public struct ItemUI
    {
        public ItemType itemType;
        public TMP_Text countText;
        public GameObject uiContainerObject;
    }

    [SerializeField] private TMP_Text cashText;
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationParent;
    [SerializeField] private List<ItemUI> itemUIList = new List<ItemUI>();

    [SerializeField] private Transform objectivePanelParent;
    [SerializeField] private GameObject objectiveUIPrefab;
    [SerializeField] private Image levelProgressBar;
    [SerializeField] private GameObject levelCompletePopup;
    [SerializeField] private TMP_Text levelText;

    [SerializeField] private RectTransform objectiveContentContainer;
    [SerializeField] private TMP_Text toggleButtonIndicatorText;
    [SerializeField] private CanvasGroup objectiveCanvasGroup;

    private int currentCash = 0;
    public int CurrentCash => currentCash;

    private Dictionary<ItemType, int> itemCounts = new Dictionary<ItemType, int>();
    private List<GameObject> notificationPool = new List<GameObject>();
    private Dictionary<Level.LevelObjective, TMP_Text> objectiveTexts = new Dictionary<Level.LevelObjective, TMP_Text>();

    private bool isObjectivePanelOpen = true;
    private bool isTransitioning = false;
    private float notificationVerticalOffset = 60f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        UpdateAllUI();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            currentCash = GameManager.Instance.Data.currentCash;
        }
        RefreshItemUIVisibility();
        UpdateAllUI();
    }

    private void OnEnable()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted += InitializeProgressBar;
            LevelManager.Instance.OnLevelStarted += SetupObjectiveUI;
            LevelManager.Instance.OnLevelStarted += RefreshItemUIVisibility;
        }

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.OnObjectiveUpdated += UpdateProgressBar;
            ObjectiveManager.Instance.OnObjectiveUpdated += UpdateObjectiveUI;
        }
    }

    private void OnDisable()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelStarted -= InitializeProgressBar;
            LevelManager.Instance.OnLevelStarted -= SetupObjectiveUI;
            LevelManager.Instance.OnLevelStarted -= RefreshItemUIVisibility;
        }

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.OnObjectiveUpdated -= UpdateProgressBar;
            ObjectiveManager.Instance.OnObjectiveUpdated -= UpdateObjectiveUI;
        }
    }

    public static string FormatMoney(int value)
    {
        if (value >= 1000000000) return (value / 1000000000f).ToString("0.#") + "B";
        if (value >= 1000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 1000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }

    private void RefreshItemUIVisibility()
    {
        int currentLevelIndex = 0;
        if (LevelManager.Instance != null)
        {
            currentLevelIndex = LevelManager.Instance.CurrentLevelIndex;
        }

        for (int i = 0; i < itemUIList.Count; i++)
        {
            ItemUI ui = itemUIList[i];
            if (ui.uiContainerObject != null)
            {
                bool shouldShow = true;

                if (currentLevelIndex == 0 && (ui.itemType == ItemType.Onion || ui.itemType == ItemType.Garlic))
                {
                    shouldShow = false;
                }

                ui.uiContainerObject.SetActive(shouldShow);
            }
        }
    }

    private void InitializeProgressBar()
    {
        isTransitioning = false;
        if (levelProgressBar != null)
        {
            levelProgressBar.DOKill();
            levelProgressBar.fillAmount = 0f;
        }
    }

    private void UpdateProgressBar()
    {
        if (ObjectiveManager.Instance == null || ObjectiveManager.Instance.currentObjectives == null) return;

        int totalObjectives = ObjectiveManager.Instance.currentObjectives.Count;
        int completedObjectives = 0;

        for (int i = 0; i < totalObjectives; i++)
        {
            if (ObjectiveManager.Instance.currentObjectives[i].IsCompleted) completedObjectives++;
        }

        if (totalObjectives > 0 && levelProgressBar != null)
        {
            float targetFill = (float)completedObjectives / totalObjectives;
            levelProgressBar.DOKill();
            levelProgressBar.DOFillAmount(targetFill, 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    public void ForceCompleteProgressBar()
    {
        if (levelProgressBar != null)
        {
            levelProgressBar.DOKill();
            levelProgressBar.DOFillAmount(1f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    private void UpdateAllUI()
    {
        if (cashText != null) cashText.text = "$" + FormatMoney(currentCash);
        for (int i = 0; i < itemUIList.Count; i++)
        {
            ItemUI ui = itemUIList[i];
            if (ui.uiContainerObject != null && !ui.uiContainerObject.activeSelf) continue;

            int count = GetItemCount(ui.itemType);
            if (ui.countText != null) ui.countText.text = FormatMoney(count);
        }
    }

    public void UpdateCashUI()
    {
        if (cashText != null)
        {
            cashText.text = "$" + FormatMoney(currentCash);
            cashText.transform.DOKill(true);
            cashText.transform.localScale = Vector3.one;
            cashText.transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0f), 0.3f, 5, 1f);
        }
    }

    public void UpdateItemUI(ItemType changedItemType)
    {
        for (int i = 0; i < itemUIList.Count; i++)
        {
            ItemUI ui = itemUIList[i];
            if (ui.itemType == changedItemType && ui.countText != null)
            {
                int count = GetItemCount(changedItemType);
                ui.countText.text = FormatMoney(count);
                ui.countText.transform.DOKill(true);
                ui.countText.transform.localScale = Vector3.one;
                ui.countText.transform.DOPunchScale(new Vector3(0f, 0.2f, 0f), 0.3f, 5, 1f);
            }
        }
    }

    public int GetItemCount(ItemType itemType)
    {
        if (itemCounts.TryGetValue(itemType, out int count)) return count;
        return 0;
    }

    public void IncrementItem(ItemType itemType)
    {
        if (isTransitioning) return;
        if (!itemCounts.ContainsKey(itemType)) itemCounts[itemType] = 0;
        itemCounts[itemType]++;

        for (int i = 0; i < itemUIList.Count; i++)
        {
            ItemUI ui = itemUIList[i];
            if (ui.itemType == itemType && ui.uiContainerObject != null && !ui.uiContainerObject.activeSelf)
            {
                ui.uiContainerObject.SetActive(true);
            }
        }

        UpdateItemUI(itemType);
    }

    public void AddCash(int amount)
    {
        if (isTransitioning) return;
        currentCash += amount;

        if (GameManager.Instance != null) GameManager.Instance.Data.currentCash = currentCash;

        UpdateCashUI();

        if (amount > 0 && ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.EarnMoney, amount);
        }
    }

    public void SpendCash(int amount)
    {
        currentCash -= amount;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Data.currentCash = currentCash;
            GameManager.Instance.SaveData();
        }
        UpdateCashUI();
    }

    private GameObject GetNotificationFromPool()
    {
        for (int i = 0; i < notificationPool.Count; i++)
        {
            GameObject notif = notificationPool[i];
            if (notif != null && !notif.activeSelf)
            {
                notif.SetActive(true);
                return notif;
            }
        }

        if (notificationPrefab == null || notificationParent == null) return null;
        GameObject newNotif = Instantiate(notificationPrefab, notificationParent);
        newNotif.SetActive(true);
        notificationPool.Add(newNotif);
        return newNotif;
    }

    public void AddNotification(string message, Color color)
    {
        GameObject notifObj = GetNotificationFromPool();
        if (notifObj == null) return;

        notifObj.SetActive(true);
        notifObj.transform.SetAsLastSibling();

        if (notifObj.TryGetComponent(out TMP_Text notifText))
        {
            notifText.text = message;
            notifText.color = new Color(color.r, color.g, color.b, 1f);
        }

        if (notifObj.TryGetComponent(out RectTransform rect))
        {
            int activeNotifCount = 0;
            foreach (GameObject notif in notificationPool)
            {
                if (notif.activeSelf && notif != notifObj)
                {
                    activeNotifCount++;
                }
            }

            float startY = activeNotifCount * notificationVerticalOffset;
            rect.anchoredPosition = new Vector2(0f, startY);
            rect.anchoredPosition += new Vector2(Random.Range(-20f, 20f), 0f);

            notifObj.transform.DOKill();
            notifObj.transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(notifObj.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
            seq.Join(rect.DOAnchorPosY(rect.anchoredPosition.y + 120f, 0.7f).SetEase(Ease.OutCubic));

            if (notifText != null) seq.Insert(0.6f, notifText.DOFade(0f, 0.6f));

            seq.OnComplete(() =>
            {
                if (notifObj != null) notifObj.SetActive(false);
            });
        }
    }

    public void UpdateFactoryWorldUI(TMP_Text uiText, bool isProcessing, List<FactoryArea.RequiredIngredient> requiredIngredients, Dictionary<ItemType, int> currentDeposited)
    {
        if (uiText == null) return;

        string display = isProcessing ? "<color=yellow>PRODUCING...</color>\n" : "";
        for (int i = 0; i < requiredIngredients.Count; i++)
        {
            var req = requiredIngredients[i];
            int current = currentDeposited.ContainsKey(req.itemType) ? currentDeposited[req.itemType] : 0;
            display += $"{req.itemType}: {FormatMoney(current)}/{FormatMoney(req.requiredAmount)}\n";
        }
        uiText.text = display;
    }

    private string GetFormattedObjectiveText(Level.LevelObjective objective)
    {
        string actionName = "";
        switch (objective.type)
        {
            case Level.ObjectiveType.HarvestItem: actionName = "Harvest"; break;
            case Level.ObjectiveType.ProduceItem: actionName = "Produce"; break;
            case Level.ObjectiveType.SellItem: actionName = "Sell"; break;
            case Level.ObjectiveType.ServeCustomer: actionName = "Serve Customer"; break;
            case Level.ObjectiveType.EarnMoney: actionName = "Earn Money"; break;
        }

        bool needsItem = objective.type == Level.ObjectiveType.HarvestItem ||
                         objective.type == Level.ObjectiveType.ProduceItem ||
                         objective.type == Level.ObjectiveType.SellItem;

        string currentAmountText = FormatMoney(objective.currentAmount);
        string targetAmountText = FormatMoney(objective.targetAmount);

        return needsItem ? $"{actionName} {objective.itemType}: \n{currentAmountText}/{targetAmountText}"
                         : $"{actionName}: \n{currentAmountText}/{targetAmountText}";
    }

    private void SetupObjectiveUI()
    {
        isTransitioning = false;
        if (objectivePanelParent == null) return;

        foreach (Transform child in objectivePanelParent)
        {
            child.DOKill(true);
            Destroy(child.gameObject);
        }

        objectiveTexts.Clear();

        if (LevelManager.Instance == null || LevelManager.Instance.currentLevelInstance == null) return;
        if (levelText != null) levelText.text = "LEVEL " + (LevelManager.Instance.CurrentLevelIndex + 1).ToString();

        Level currentLevel = LevelManager.Instance.currentLevelInstance;

        for (int i = 0; i < currentLevel.objectives.Count; i++)
        {
            var objective = currentLevel.objectives[i];
            if (objectiveUIPrefab == null) continue;
            GameObject objUI = Instantiate(objectiveUIPrefab, objectivePanelParent);
            TMP_Text textComp = objUI.GetComponentInChildren<TMP_Text>();

            if (textComp != null)
            {
                textComp.text = GetFormattedObjectiveText(objective);
                if (!objectiveTexts.ContainsKey(objective)) objectiveTexts.Add(objective, textComp);
            }
        }

        if (levelProgressBar != null) levelProgressBar.fillAmount = 0f;
        if (levelCompletePopup != null) levelCompletePopup.SetActive(false);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(objectivePanelParent.GetComponent<RectTransform>());
    }

    private void UpdateObjectiveUI()
    {
        if (ObjectiveManager.Instance == null || ObjectiveManager.Instance.currentObjectives == null) return;
        List<Level.LevelObjective> currentObjectives = ObjectiveManager.Instance.currentObjectives;

        List<Level.LevelObjective> completedToRemove = new List<Level.LevelObjective>();

        for (int i = 0; i < currentObjectives.Count; i++)
        {
            var objective = currentObjectives[i];
            if (objectiveTexts.TryGetValue(objective, out TMP_Text textComp) && textComp != null)
            {
                string newText = GetFormattedObjectiveText(objective);
                bool textChanged = textComp.text != newText;

                if (textChanged)
                {
                    textComp.text = newText;
                }

                if (objective.IsCompleted)
                {
                    Transform rootUI = textComp.transform;
                    while (rootUI.parent != null && rootUI.parent != objectivePanelParent)
                    {
                        rootUI = rootUI.parent;
                    }

                    rootUI.DOKill();
                    textComp.transform.DOKill();

                    rootUI.localScale = Vector3.one;

                    Sequence seq = DOTween.Sequence();
                    seq.SetUpdate(true);

                    textComp.color = Color.green;

                    if (SoundManager.Instance != null && SoundManager.Instance.objectiveCompleteClip != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.Instance.objectiveCompleteClip, 1f);
                    }

                    seq.Append(rootUI.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.25f, 2, 0.5f));
                    seq.AppendInterval(0.2f);
                    seq.Append(rootUI.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
                    seq.OnComplete(() =>
                    {
                        if (rootUI != null) Destroy(rootUI.gameObject);
                    });

                    completedToRemove.Add(objective);
                }
                else if (textChanged)
                {
                    textComp.transform.DOKill();
                    textComp.transform.localScale = Vector3.one;
                    textComp.transform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), 0.15f, 2, 0.5f).SetUpdate(true);
                }
            }
        }

        for (int i = 0; i < completedToRemove.Count; i++)
        {
            objectiveTexts.Remove(completedToRemove[i]);
        }
    }

    public void ShowLevelCompletePopup()
    {
        isTransitioning = true;
        if (levelCompletePopup != null)
        {
            levelCompletePopup.transform.DOKill();
            levelCompletePopup.transform.localScale = Vector3.zero;
            levelCompletePopup.SetActive(true);
            levelCompletePopup.transform.DOScale(Vector3.one, 0.6f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    public void UpdateLevelText(int levelNumber)
    {
        if (levelText != null) levelText.text = "LEVEL " + levelNumber.ToString();
    }

    public void ResetProgress()
    {
        itemCounts.Clear();
        UpdateAllUI();
    }

    public void TriggerLevelCompleteUI()
    {
        ShowLevelCompletePopup();
        ForceCompleteProgressBar();
    }

    public void ToggleObjectivePanel()
    {
        if (objectivePanelParent == null) return;
        Transform targetPanelToToggle = objectivePanelParent.parent;
        if (targetPanelToToggle == null) targetPanelToToggle = objectivePanelParent;

        RectTransform panelRect = targetPanelToToggle.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = targetPanelToToggle.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = targetPanelToToggle.gameObject.AddComponent<CanvasGroup>();
        }

        isObjectivePanelOpen = !isObjectivePanelOpen;

        panelRect.DOKill();
        canvasGroup.DOKill();

        if (isObjectivePanelOpen)
        {
            targetPanelToToggle.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, 0.3f).SetUpdate(true);
            panelRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

            if (toggleButtonIndicatorText != null)
            {
                toggleButtonIndicatorText.text = "^";
                toggleButtonIndicatorText.color = Color.white;
            }
        }
        else
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, 0.25f).SetUpdate(true);
            panelRect.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).SetUpdate(true);

            if (toggleButtonIndicatorText != null)
            {
                toggleButtonIndicatorText.text = "--";
                toggleButtonIndicatorText.color = Color.white;
            }
        }
    }
}
