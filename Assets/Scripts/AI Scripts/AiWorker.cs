using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class AiWorker : MonoBehaviour
{
    public enum AiState { Searching, MovingToCrop, MovingToStock, Unloading }

    public AiState currentState = AiState.Searching;
    public Transform backpackParent;
    public Animator animator;

    [HideInInspector] public GameObject targetCrop;
    [HideInInspector] public ItemType assignedTargetItemType = ItemType.Tomato;
    [HideInInspector] public Field.CropArea myClaimedArea = null;

    private NavMeshAgent agent;
    private List<GameObject> collectedItems = new List<GameObject>();
    private float itemSpacingBuffer = 0.1f;
    private float cachedItemHeight = -1f;

    private StockArea targetStockArea;
    private Transform currentStockPointTarget;
    private Transform currentTargetTransform;

    private bool isUnloadingPhase = false;
    private int currentActiveCapacity;
    private Coroutine unloadCoroutine;
    private int searchFailCount = 0;

    private float interactionDistance = 1.0f;
    private float pathUpdateTimer = 0f;
    private float stuckTimer = 0f;

    private void Start()
    {
        if (TryGetComponent(out agent))
        {
            agent.enabled = true;
            agent.updateRotation = true;
            agent.speed = 4.0f;
            agent.angularSpeed = 1200f;
            agent.acceleration = 120f;
            agent.stoppingDistance = 0.2f;
            agent.radius = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.autoRepath = true;
            agent.avoidancePriority = Random.Range(30, 70);

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (AiManager.Instance != null) AiManager.Instance.RegisterAi(this);

        currentActiveCapacity = GetTargetCapacity();
        EnterState(currentState);
        StartCoroutine(AiBrainRoutine());
    }

    public void SetTargetItemType(ItemType type)
    {
        assignedTargetItemType = type;
        cachedItemHeight = -1f;
    }

    private int GetTargetCapacity()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.Data.aiCapacityUpgradeLevel : 0;
        int safeBaseCapacity = 3;
        int safePerLevel = 1;

        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            safeBaseCapacity = Mathf.Max(3, LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.startingAiCapacity);
            safePerLevel = Mathf.Max(1, LevelManager.Instance.CurrentLevelConfig.playerAndAiSettings.capacityIncreasePerAiLevel);
        }

        return safeBaseCapacity + (level * safePerLevel);
    }

    private IEnumerator AiBrainRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(0.15f, 0.25f));

            if (animator != null && agent != null && agent.isOnNavMesh)
            {
                bool isWalking = agent.velocity.sqrMagnitude > 0.05f;
                if (animator.GetBool("isMoving") != isWalking)
                {
                    animator.SetBool("isMoving", isWalking);
                }
            }

            switch (currentState)
            {
                case AiState.Searching:
                    UpdateSearchingState();
                    break;
                case AiState.MovingToCrop:
                    UpdateMovingToCropState();
                    break;
                case AiState.MovingToStock:
                    UpdateMovingToStockState();
                    break;
            }
        }
    }

    private void ChangeState(AiState newState)
    {
        if (currentState == newState) return;

        ExitState(currentState);
        currentState = newState;
        EnterState(currentState);
    }

    private void EnterState(AiState state)
    {
        currentTargetTransform = null;
        pathUpdateTimer = 0f;
        stuckTimer = 0f;

        switch (state)
        {
            case AiState.Searching:
                targetCrop = null;
                targetStockArea = null;
                currentStockPointTarget = null;
                break;
            case AiState.MovingToCrop:
                if (targetCrop != null) MoveToTarget(targetCrop.transform);
                break;
            case AiState.MovingToStock:
                if (currentStockPointTarget != null) MoveToTarget(currentStockPointTarget);
                break;
            case AiState.Unloading:
                StopAgentCompletely();
                unloadCoroutine = StartCoroutine(UnloadRoutine());
                break;
        }
    }

    private void ExitState(AiState state)
    {
        if (state == AiState.Unloading && unloadCoroutine != null)
        {
            StopCoroutine(unloadCoroutine);
        }
    }

    private void UpdateSearchingState()
    {
        if (collectedItems.Count == 0) currentActiveCapacity = GetTargetCapacity();

        if (collectedItems.Count >= currentActiveCapacity)
        {
            isUnloadingPhase = true;
        }

        if (isUnloadingPhase)
        {
            if (collectedItems.Count == 0)
            {
                isUnloadingPhase = false;
            }
            else
            {
                FindTargetStockArea();
                if (targetStockArea == null) StopAgentCompletely();
                return;
            }
        }

        targetCrop = FindBestValidCrop();

        if (targetCrop != null)
        {
            ChangeState(AiState.MovingToCrop);
        }
        else if (myClaimedArea != null && !isUnloadingPhase)
        {
            Transform standbyPoint = null;
            if (myClaimedArea.dirtPoints != null && myClaimedArea.dirtPoints.Count > 0)
            {
                standbyPoint = myClaimedArea.dirtPoints[0];
            }

            if (standbyPoint != null)
            {
                if (Vector3.Distance(transform.position, standbyPoint.position) > 1.5f)
                {
                    MoveToTarget(standbyPoint);
                }
                else
                {
                    StopAgentCompletely();
                }
            }
        }
        else
        {
            StopAgentCompletely();
        }
    }

    private void TryClaimAnArea()
    {
        List<Field> fields = null;
        if (FieldManager.Instance != null && FieldManager.Instance.activeFields != null && FieldManager.Instance.activeFields.Count > 0)
        {
            fields = FieldManager.Instance.activeFields;
        }
        else
        {
            fields = new List<Field>(FindObjectsOfType<Field>(true));
        }

        if (fields == null || fields.Count == 0) return;

        Field.CropArea bestArea = null;
        int minWorkers = int.MaxValue;

        for (int i = 0; i < fields.Count; i++)
        {
            Field mainField = fields[i];
            if (mainField == null || mainField.cropAreas == null) continue;

            for (int c = 0; c < mainField.cropAreas.Count; c++)
            {
                Field.CropArea area = mainField.cropAreas[c];

                if (area == null) continue;
                if (area.cachedUnlockArea != null && area.cachedUnlockArea.gameObject.activeInHierarchy) continue;
                if (area.cachedItemType != assignedTargetItemType) continue;

                int workerCount = CountWorkersInArea(area);
                if (workerCount < minWorkers)
                {
                    minWorkers = workerCount;
                    bestArea = area;
                }
            }
        }

        if (bestArea != null)
        {
            myClaimedArea = bestArea;
        }
    }

    private int CountWorkersInArea(Field.CropArea area)
    {
        int count = 0;
        if (AiManager.Instance == null || AiManager.Instance.activeAiWorkers == null) return count;

        List<AiWorker> workers = AiManager.Instance.activeAiWorkers;
        for (int i = 0; i < workers.Count; i++)
        {
            AiWorker worker = workers[i];
            if (worker != null && worker != this && worker.myClaimedArea == area)
            {
                count++;
            }
        }
        return count;
    }

    private GameObject FindBestValidCrop()
    {
        if (myClaimedArea == null)
        {
            TryClaimAnArea();
        }

        if (myClaimedArea != null && myClaimedArea.cachedUnlockArea != null && myClaimedArea.cachedUnlockArea.gameObject.activeInHierarchy)
        {
            myClaimedArea = null;
        }

        GameObject nearestCrop = null;
        float minDistance = Mathf.Infinity;
        Vector3 seekerPos = transform.position;

        if (myClaimedArea != null && myClaimedArea.dirtPoints != null)
        {
            for (int j = 0; j < myClaimedArea.dirtPoints.Count; j++)
            {
                Transform dirt = myClaimedArea.dirtPoints[j];
                if (dirt == null) continue;

                if (myClaimedArea.dirtOccupancy != null && myClaimedArea.dirtOccupancy.TryGetValue(dirt, out GameObject crop) && crop != null && crop.activeInHierarchy)
                {
                    if (crop.TryGetComponent(out Item itemComp) && !itemComp.isCollected)
                    {
                        float dist = (seekerPos - crop.transform.position).sqrMagnitude;
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            nearestCrop = crop;
                        }
                    }
                }
            }
        }

        if (nearestCrop == null)
        {
            List<Field> fields = null;
            if (FieldManager.Instance != null && FieldManager.Instance.activeFields != null && FieldManager.Instance.activeFields.Count > 0)
            {
                fields = FieldManager.Instance.activeFields;
            }
            else
            {
                fields = new List<Field>(FindObjectsOfType<Field>(true));
            }

            if (fields != null)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    Field mainField = fields[i];
                    if (mainField == null || mainField.cropAreas == null) continue;

                    for (int c = 0; c < mainField.cropAreas.Count; c++)
                    {
                        Field.CropArea area = mainField.cropAreas[c];
                        if (area == null || area.dirtPoints == null) continue;
                        if (area.cachedUnlockArea != null && area.cachedUnlockArea.gameObject.activeInHierarchy) continue;
                        if (area.cachedItemType != assignedTargetItemType) continue;

                        for (int j = 0; j < area.dirtPoints.Count; j++)
                        {
                            Transform dirt = area.dirtPoints[j];
                            if (dirt == null) continue;

                            if (area.dirtOccupancy != null && area.dirtOccupancy.TryGetValue(dirt, out GameObject crop) && crop != null && crop.activeInHierarchy)
                            {
                                if (crop.TryGetComponent(out Item itemComp) && !itemComp.isCollected)
                                {
                                    float dist = (seekerPos - crop.transform.position).sqrMagnitude;
                                    if (dist < minDistance)
                                    {
                                        minDistance = dist;
                                        nearestCrop = crop;
                                        myClaimedArea = area;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return nearestCrop;
    }

    private void UpdateMovingToCropState()
    {
        if (targetCrop == null || !targetCrop.activeSelf)
        {
            StopAgentCompletely();
            targetCrop = null;
            ChangeState(AiState.Searching);
            return;
        }

        if (targetCrop.TryGetComponent(out Item itemComp) && itemComp.isCollected)
        {
            StopAgentCompletely();
            targetCrop = null;
            ChangeState(AiState.Searching);
            return;
        }

        MoveToTarget(targetCrop.transform);

        Vector3 pos2D = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 target2D = new Vector3(targetCrop.transform.position.x, 0, targetCrop.transform.position.z);

        float currentDistance = Vector3.Distance(pos2D, target2D);

        if (currentDistance <= interactionDistance)
        {
            stuckTimer = 0f;
            StopAgentCompletely();
            CollectCrop(targetCrop);
            ChangeState(AiState.Searching);
            return;
        }

        if (agent != null && !agent.pathPending && agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += 0.2f;
            if (stuckTimer >= 1.0f)
            {
                if (currentDistance <= 2.5f)
                {
                    stuckTimer = 0f;
                    StopAgentCompletely();
                    CollectCrop(targetCrop);
                    ChangeState(AiState.Searching);
                }
                else
                {
                    stuckTimer = 0f;
                    StopAgentCompletely();
                    targetCrop = null;
                    ChangeState(AiState.Searching);
                }
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void UpdateMovingToStockState()
    {
        if (targetStockArea == null || currentStockPointTarget == null)
        {
            StopAgentCompletely();
            ChangeState(AiState.Searching);
            return;
        }

        MoveToTarget(currentStockPointTarget);

        Vector3 pos2D = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 target2D = new Vector3(currentStockPointTarget.position.x, 0, currentStockPointTarget.position.z);

        float currentDistance = Vector3.Distance(pos2D, target2D);

        if (currentDistance <= interactionDistance)
        {
            stuckTimer = 0f;
            StopAgentCompletely();
            ChangeState(AiState.Unloading);
            return;
        }

        if (agent != null && !agent.pathPending && agent.velocity.sqrMagnitude < 0.1f)
        {
            stuckTimer += 0.2f;
            if (stuckTimer >= 1.0f)
            {
                if (currentDistance <= 3.0f)
                {
                    stuckTimer = 0f;
                    StopAgentCompletely();
                    ChangeState(AiState.Unloading);
                }
                else
                {
                    stuckTimer = 0f;
                    StopAgentCompletely();
                    ChangeState(AiState.Searching);
                }
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void MoveToTarget(Transform target)
    {
        if (agent == null || target == null) return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        if (agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            pathUpdateTimer -= 0.2f;
            if (currentTargetTransform != target || pathUpdateTimer <= 0f)
            {
                currentTargetTransform = target;
                if (agent.isStopped) agent.isStopped = false;
                agent.SetDestination(target.position);
                pathUpdateTimer = 0.5f;
            }
        }
    }

    private void StopAgentCompletely()
    {
        if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (agent.hasPath) agent.ResetPath();
        }
    }

    private void FindTargetStockArea()
    {
        if (collectedItems.Count == 0) return;

        ItemType holdingType = assignedTargetItemType;
        if (collectedItems[collectedItems.Count - 1].TryGetComponent(out Item holdItem))
        {
            holdingType = holdItem.ItemType;
        }

        targetStockArea = null;
        currentStockPointTarget = null;

        List<StockArea> stocks = null;
        if (StockManager.Instance != null && StockManager.Instance.activeStockAreas != null && StockManager.Instance.activeStockAreas.Count > 0)
        {
            stocks = StockManager.Instance.activeStockAreas;
        }
        else
        {
            stocks = new List<StockArea>(FindObjectsOfType<StockArea>(true));
        }

        if (stocks == null || stocks.Count == 0)
        {
            searchFailCount++;
            if (searchFailCount >= 3)
            {
                ChangeState(AiState.Searching);
                searchFailCount = 0;
            }
            return;
        }

        for (int i = 0; i < stocks.Count; i++)
        {
            StockArea stock = stocks[i];
            if (stock != null && stock.gameObject.activeInHierarchy && stock.isActiveAndEnabled && stock.expectedItemType == holdingType && stock.HasEmptyStockPoint())
            {
                Transform pointChild = stock.fillPoint != null ? stock.fillPoint : stock.transform;

                if (pointChild != null)
                {
                    targetStockArea = stock;
                    currentStockPointTarget = pointChild;
                    searchFailCount = 0;
                    ChangeState(AiState.MovingToStock);
                    return;
                }
            }
        }

        searchFailCount++;
        if (searchFailCount >= 3)
        {
            ChangeState(AiState.Searching);
            searchFailCount = 0;
        }
    }

    private float GetItemRealHeight(GameObject itemObj)
    {
        if (cachedItemHeight > 0f) return cachedItemHeight;

        if (itemObj.TryGetComponent(out Collider col))
        {
            if (col is BoxCollider box) cachedItemHeight = box.size.y * itemObj.transform.localScale.y;
            else if (col is SphereCollider sphere) cachedItemHeight = sphere.radius * 2f * itemObj.transform.localScale.y;
            else if (col is CapsuleCollider capsule) cachedItemHeight = capsule.height * itemObj.transform.localScale.y;
        }

        if (cachedItemHeight <= 0f) cachedItemHeight = 0.25f;
        return cachedItemHeight;
    }

    private float CalculateCurrentStackHeight()
    {
        if (collectedItems.Count == 0) return 0f;

        float itemHeight = GetItemRealHeight(collectedItems[0]);
        return collectedItems.Count * (itemHeight + itemSpacingBuffer);
    }

    private void CollectCrop(GameObject cropObject)
    {
        if (!cropObject.TryGetComponent(out Item itemComp) || itemComp.isCollected) return;

        itemComp.isCollected = true;
        cropObject.tag = "Untagged";

        if (cropObject.TryGetComponent(out Collider col)) col.enabled = false;

        if (FieldManager.Instance != null) FieldManager.Instance.ReportItemCollected(cropObject);

        if (backpackParent != null)
        {
            cropObject.transform.SetParent(backpackParent, true);
            Vector3 prefabScale = itemComp.ItemPrefab != null ? itemComp.ItemPrefab.transform.localScale : Vector3.one;

            float dynamicStartY = CalculateCurrentStackHeight();
            float itemHeight = GetItemRealHeight(cropObject);
            Vector3 localTargetPos = new Vector3(0, dynamicStartY + (itemHeight / 2f), 0);

            cropObject.transform.DOKill();
            cropObject.transform.DOLocalJump(localTargetPos, 1.0f, 1, 0.25f).OnComplete(() =>
            {
                if (cropObject != null)
                {
                    cropObject.transform.localPosition = localTargetPos;
                    cropObject.transform.localRotation = Quaternion.identity;
                    cropObject.transform.localScale = prefabScale;
                }
            });
        }
        collectedItems.Add(cropObject);
    }

    private IEnumerator UnloadRoutine()
    {
        Image fillImg = currentStockPointTarget != null ? currentStockPointTarget.GetComponentInChildren<Image>() : null;
        float fillDuration = 0.5f;

        while (collectedItems.Count > 0 && targetStockArea != null && targetStockArea.HasEmptyStockPoint())
        {
            GameObject nextItemToCheck = collectedItems[collectedItems.Count - 1];

            if (nextItemToCheck.TryGetComponent(out Item nextItemComp) && nextItemComp.ItemType != targetStockArea.expectedItemType)
            {
                if (fillImg != null) fillImg.fillAmount = 0f;
                ChangeState(AiState.Searching);
                yield break;
            }

            StopAgentCompletely();
            float currentFillTime = 0f;

            while (currentFillTime < fillDuration)
            {
                if (targetStockArea == null || !targetStockArea.isActiveAndEnabled || !targetStockArea.HasEmptyStockPoint() || collectedItems.Count == 0)
                {
                    if (fillImg != null) fillImg.fillAmount = 0f;
                    ChangeState(AiState.Searching);
                    yield break;
                }

                currentFillTime += Time.deltaTime;
                if (fillImg != null) fillImg.fillAmount = Mathf.Clamp01(currentFillTime / fillDuration);
                yield return null;
            }

            if (fillImg != null) fillImg.fillAmount = 0f;

            Transform actualStockPoint = targetStockArea.GetNextStockPoint();
            if (actualStockPoint == null) break;

            GameObject itemToDrop = collectedItems[collectedItems.Count - 1];
            if (!itemToDrop.TryGetComponent(out Item itemComp)) break;

            collectedItems.RemoveAt(collectedItems.Count - 1);

            float itemHeight = GetItemRealHeight(itemToDrop);
            itemToDrop.transform.SetParent(null, true);
            Vector3 dropPosition = actualStockPoint.position + new Vector3(0, itemHeight / 2f, 0);

            StockArea lockedStockArea = targetStockArea;
            Transform lockedStockPoint = actualStockPoint;
            ItemType lockedItemType = itemComp.ItemType;
            GameObject lockedItemPrefab = itemComp.ItemPrefab;

            itemToDrop.transform.DOKill();
            itemToDrop.transform.DOJump(dropPosition, 2f, 1, 0.4f).OnComplete(() =>
            {
                if (itemToDrop != null)
                {
                    itemToDrop.transform.position = dropPosition;
                    itemToDrop.transform.rotation = lockedStockPoint.rotation;
                    lockedStockArea.AddItemToInspectorList(lockedStockPoint, lockedItemType, lockedItemPrefab, itemToDrop);
                }
            });

            yield return new WaitForSeconds(0.15f);
        }

        if (fillImg != null) fillImg.fillAmount = 0f;

        if (collectedItems.Count == 0) currentActiveCapacity = GetTargetCapacity();

        ChangeState(AiState.Searching);
    }
}
