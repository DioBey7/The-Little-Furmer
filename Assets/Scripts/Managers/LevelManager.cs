using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DG.Tweening;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    public LevelDataSO masterLevelData;

    private int currentLevelIndex = 0;
    public int CurrentLevelIndex => currentLevelIndex;

    public LevelConfig CurrentLevelConfig { get; private set; }

    [HideInInspector]
    public Level currentLevelInstance;

    public event Action OnLevelStarted;
    public event Action OnLevelCompleted;

    private bool isLevelCompleting = false;

    private ItemCollecting cachedPlayer;
    private Vector3 initialPlayerPosition;
    private List<int> currentSavedProgress = new List<int>();

    private static readonly ItemType[] cachedItemTypes = (ItemType[])Enum.GetValues(typeof(ItemType));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayer = playerObj.GetComponent<ItemCollecting>();
            if (cachedPlayer != null) initialPlayerPosition = cachedPlayer.transform.position;
        }
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            LoadLevel(0, new List<int>());
        }
    }

    public void LoadLevel(int index, List<int> savedProgress)
    {
        currentLevelIndex = index;
        currentSavedProgress = savedProgress;
        StartCoroutine(LoadLevelRoutine(index));
    }

    private IEnumerator LoadLevelRoutine(int index)
    {
        isLevelCompleting = false;

        CleanupScene();
        yield return null;

        if (masterLevelData != null && masterLevelData.levels.Count > 0)
        {
            int dataIndex = index % masterLevelData.levels.Count;
            CurrentLevelConfig = masterLevelData.levels[dataIndex];

            if (currentLevelInstance != null)
            {
                Destroy(currentLevelInstance.gameObject);
                currentLevelInstance = null;
            }

            if (CurrentLevelConfig.levelPrefab != null)
            {
                Vector3 spawnPos = CurrentLevelConfig.levelPrefab.transform.position;
                Quaternion spawnRot = CurrentLevelConfig.levelPrefab.transform.rotation;

                GameObject levelObj = Instantiate(CurrentLevelConfig.levelPrefab.gameObject, spawnPos, spawnRot);
                currentLevelInstance = levelObj.GetComponent<Level>();
            }

            if (currentLevelInstance != null)
            {
                if (CurrentLevelConfig.inspectorObjectives != null && CurrentLevelConfig.inspectorObjectives.Count > 0)
                {
                    ApplyManualObjectives(CurrentLevelConfig);
                }
                else
                {
                    currentLevelInstance.GenerateDynamicObjectives(index, CurrentLevelConfig);
                }

                if (ObjectiveManager.Instance != null)
                {
                    ObjectiveManager.Instance.InitializeObjectives(currentLevelInstance.objectives, currentSavedProgress);
                }
            }

            InjectLevelDependencies();
        }
    }

    private void ApplyManualObjectives(LevelConfig data)
    {
        if (currentLevelInstance == null) return;
        currentLevelInstance.objectives.Clear();
        foreach (var manualObj in data.inspectorObjectives)
        {
            Level.LevelObjective newObj = new Level.LevelObjective();
            newObj.type = manualObj.type;
            newObj.itemType = manualObj.itemType;
            newObj.targetAmount = manualObj.targetAmount;
            newObj.currentAmount = 0;
            currentLevelInstance.objectives.Add(newObj);
        }
    }

    private void CleanupScene()
    {
        if (cachedPlayer != null)
        {
            cachedPlayer.ResetBackpack();
            cachedPlayer.transform.position = initialPlayerPosition;

            Rigidbody rb = cachedPlayer.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero;
        }

        if (CashManager.Instance != null)
        {
            CashItem[] activeCash = FindObjectsOfType<CashItem>(true);
            for (int i = 0; i < activeCash.Length; i++)
            {
                if (activeCash[i] != null)
                {
                    activeCash[i].transform.DOKill(true);
                    CashManager.Instance.ReturnToPool(activeCash[i].gameObject);
                }
            }
        }

        if (PoolingManager.Instance != null)
        {
            Item[] activeItems = FindObjectsOfType<Item>(true);
            for (int i = 0; i < activeItems.Length; i++)
            {
                if (activeItems[i] != null)
                {
                    activeItems[i].transform.DOKill(true);
                    activeItems[i].isCollected = false;
                    PoolingManager.Instance.ReturnItem(activeItems[i].ItemType, activeItems[i].gameObject);
                }
            }
        }

        AiWorker[] activeAi = FindObjectsOfType<AiWorker>(true);
        for (int i = 0; i < activeAi.Length; i++)
        {
            if (activeAi[i] != null)
            {
                activeAi[i].transform.DOKill(true);
                Destroy(activeAi[i].gameObject);
            }
        }

        if (AiManager.Instance != null && AiManager.Instance.activeAiWorkers != null)
        {
            AiManager.Instance.activeAiWorkers.Clear();
        }

        if (currentLevelInstance != null)
        {
            foreach (var factory in currentLevelInstance.factoryAreas)
            {
                if (factory != null) factory.ResetFactory();
            }

            foreach (var stock in currentLevelInstance.stockAreas)
            {
                if (stock != null) stock.ResetStock();
            }
        }

        if (FieldManager.Instance != null && currentLevelInstance != null)
        {
            FieldManager.Instance.InitializeFields(currentLevelInstance.fields);
        }

        if (UiManager.Instance != null) UiManager.Instance.ResetProgress();
    }

    private void InjectLevelDependencies()
    {
        if (FieldManager.Instance != null && currentLevelInstance != null)
            FieldManager.Instance.InitializeFields(currentLevelInstance.fields);

        if (StockManager.Instance != null && currentLevelInstance != null)
            StockManager.Instance.InitializeStocks(currentLevelInstance.stockAreas);

        if (CurrentLevelConfig != null && currentLevelInstance != null)
        {
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.spawnPoint = currentLevelInstance.customerSpawnPoint;
                CustomerManager.Instance.marketArea = currentLevelInstance.marketArea;
                CustomerManager.Instance.exitPoint = currentLevelInstance.customerExitPoint;

                CustomerManager.Instance.SetLevelParameters(CurrentLevelConfig);
            }

            foreach (var factory in currentLevelInstance.factoryAreas)
            {
                if (factory != null)
                {
                    factory.SetSpeedMultiplier(CurrentLevelConfig.factorySettings.overallProductionSpeedMultiplier);
                }
            }
        }

        if (AiManager.Instance != null && currentLevelInstance != null)
            AiManager.Instance.spawnPoint = currentLevelInstance.aiCharacterSpawnPoint;

        if (UiManager.Instance != null)
        {
            if (UiManager.Instance.CurrentCash > 0)
            {
                UiManager.Instance.SpendCash(UiManager.Instance.CurrentCash);
            }

            UiManager.Instance.UpdateLevelText(currentLevelIndex + 1);
        }

        GameManager.Instance.SetGameState(true);
        OnLevelStarted?.Invoke();
    }

    public void HandleAllObjectivesCompleted()
    {
        if (isLevelCompleting) return;
        isLevelCompleting = true;
        StartCoroutine(LevelCompletedRoutine());
    }

    private IEnumerator LevelCompletedRoutine()
    {
        GameManager.Instance.SetGameState(false);

        yield return new WaitForSeconds(0.6f);

        if (UiManager.Instance != null)
        {
            UiManager.Instance.TriggerLevelCompleteUI();
        }

        OnLevelCompleted?.Invoke();

        if (SoundManager.Instance != null && SoundManager.Instance.levelUpClip != null)
        {
            SoundManager.Instance.PlaySFX(SoundManager.Instance.levelUpClip, 1f);
        }
    }

    public void LoadNextLevelFromUI()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CompleteLevel();
        }
        else
        {
            currentLevelIndex++;
            LoadLevel(currentLevelIndex, new List<int>());
        }
    }
}
