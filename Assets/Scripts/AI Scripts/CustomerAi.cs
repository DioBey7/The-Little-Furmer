using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAI : MonoBehaviour
{
    public enum CustomerState { WalkingToQueue, Waiting, Leaving }

    [System.Serializable]
    public class RequestedItem
    {
        public ItemType itemType;
        public int amount;
    }

    [HideInInspector] public List<RequestedItem> requestedItems = new List<RequestedItem>();
    public TMP_Text headText;
    [HideInInspector] public CustomerState currentState = CustomerState.WalkingToQueue;

    private float maxPatience = 60f;
    private float currentPatience;
    private int cashReward = 0;

    private NavMeshAgent agent;
    private Animator animator;
    private MarketArea marketArea;
    private int queueIndex = -1;
    private Transform exitPoint;

    private static readonly string[] happyPhrases = { "<color=#2ecc71>Awesome!</color>", "<color=#2ecc71>Thank you!</color>", "<color=#2ecc71>Perfect!</color>", "<color=#2ecc71>Great items!</color>", "<color=#2ecc71>Nice!</color>", "<color=#2ecc71>Excellent!</color>" };
    private static readonly string[] angryPhrases = { "<color=#e74c3c>Too slow!</color>", "<color=#e74c3c>I'm leaving!</color>", "<color=#e74c3c>Awful service!</color>", "<color=#e74c3c>Never mind!</color>", "<color=#e74c3c>Disappointing.</color>", "<color=#e74c3c>Terrible!</color>" };

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        if (agent != null)
        {
            agent.radius = 0.3f;
            agent.angularSpeed = 300f;
            agent.avoidancePriority = Random.Range(30, 70);
        }

        if (TryGetComponent(out CapsuleCollider col))
        {
            col.radius = 0.3f;
        }
    }

    public void Initialize(MarketArea area, int index, Transform targetPoint, Transform exitTransform, List<RequestedItem> assignedRequests)
    {
        marketArea = area;
        queueIndex = index;
        exitPoint = exitTransform;
        requestedItems = assignedRequests;

        cashReward = 0;
        for (int i = 0; i < requestedItems.Count; i++)
        {
            RequestedItem req = requestedItems[i];
            int itemPrice = 10;

            switch (req.itemType)
            {
                case ItemType.Carrot: itemPrice = 100; break;
                case ItemType.Potato: itemPrice = 150; break;
                case ItemType.Onion: itemPrice = 200; break;
                case ItemType.Garlic: itemPrice = 100; break;
                case ItemType.Tomato: itemPrice = 50; break;
            }

            cashReward += (itemPrice * req.amount);
        }

        UpdateHeadUI();
        currentPatience = maxPatience;

        if (headText != null && headText.transform.parent != null)
        {
            headText.transform.parent.gameObject.SetActive(true);
        }

        if (agent != null && targetPoint != null)
        {
            agent.SetDestination(targetPoint.position);
            currentState = CustomerState.WalkingToQueue;
        }

        StartCoroutine(CustomerBrainRoutine());
    }

    private IEnumerator CustomerBrainRoutine()
    {
        WaitForSeconds tick = new WaitForSeconds(0.15f);

        while (currentState != CustomerState.Leaving)
        {
            if (agent != null && animator != null)
            {
                bool isMoving = agent.velocity.sqrMagnitude > 0.01f;
                if (animator.GetBool("isMoving") != isMoving)
                {
                    animator.SetBool("isMoving", isMoving);
                }
            }

            switch (currentState)
            {
                case CustomerState.WalkingToQueue:
                    if (agent != null && !agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.2f))
                    {
                        currentState = CustomerState.Waiting;
                    }
                    break;

                case CustomerState.Waiting:
                    if (queueIndex == 0)
                    {
                        currentPatience -= 0.15f;
                        if (currentPatience <= 0f) Leave(false);
                    }
                    break;
            }

            yield return tick;
        }

        while (currentState == CustomerState.Leaving)
        {
            if (agent != null && animator != null)
            {
                bool isMoving = agent.velocity.sqrMagnitude > 0.01f;
                if (animator.GetBool("isMoving") != isMoving)
                {
                    animator.SetBool("isMoving", isMoving);
                }
            }

            if (agent != null && !agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.2f))
            {
                if (CustomerManager.Instance != null) CustomerManager.Instance.RemoveCustomerFromList(this);
                Destroy(gameObject);
                yield break;
            }

            yield return tick;
        }
    }

    public void UpdateQueuePosition(int newIndex, Transform targetPoint)
    {
        queueIndex = newIndex;
        if (agent != null && targetPoint != null && currentState != CustomerState.Leaving)
        {
            agent.SetDestination(targetPoint.position);
            currentState = CustomerState.WalkingToQueue;
        }
    }

    public bool ReceiveItem(ItemType type)
    {
        if (currentState == CustomerState.Leaving) return false;

        for (int i = 0; i < requestedItems.Count; i++)
        {
            RequestedItem req = requestedItems[i];
            if (req.itemType == type && req.amount > 0)
            {
                req.amount--;
                UpdateHeadUI();
                currentPatience = maxPatience;

                if (ObjectiveManager.Instance != null)
                {
                    ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.SellItem, 1, type);
                }

                CheckIfCompleted();
                return true;
            }
        }
        return false;
    }

    private void CheckIfCompleted()
    {
        bool allDone = true;
        for (int i = 0; i < requestedItems.Count; i++)
        {
            if (requestedItems[i].amount > 0)
            {
                allDone = false;
                break;
            }
        }

        if (allDone)
        {
            Leave(true);

            if (UiManager.Instance != null)
            {
                UiManager.Instance.AddCash(cashReward);
                UiManager.Instance.AddNotification("+$" + UiManager.FormatMoney(cashReward), Color.green);
            }
        }
    }

    private void UpdateHeadUI()
    {
        if (headText == null) return;

        string newText = "";
        for (int i = 0; i < requestedItems.Count; i++)
        {
            RequestedItem req = requestedItems[i];
            if (req.amount > 0)
            {
                newText += req.itemType.ToString() + " x" + req.amount + "\n";
            }
        }
        headText.text = Bubble(newText.TrimEnd('\n'));
    }

    private void Leave(bool isSatisfied)
    {
        if (currentState == CustomerState.Leaving) return;

        currentState = CustomerState.Leaving;

        if (headText != null)
        {
            if (isSatisfied)
            {
                headText.text = Bubble(happyPhrases[Random.Range(0, happyPhrases.Length)]);
                if (ObjectiveManager.Instance != null)
                {
                    ObjectiveManager.Instance.ProgressObjective(Level.ObjectiveType.ServeCustomer, 1);
                }
            }
            else
            {
                headText.text = Bubble(angryPhrases[Random.Range(0, angryPhrases.Length)]);
            }
        }

        if (marketArea != null)
        {
            marketArea.RemoveCustomerFromQueue(this);
        }

        if (agent != null && exitPoint != null)
        {
            agent.SetDestination(exitPoint.position);
        }
        else
        {
            if (CustomerManager.Instance != null) CustomerManager.Instance.RemoveCustomerFromList(this);
            Destroy(gameObject);
        }
    }

    // Order text: bold capitals with the outlined font so it reads over grass and other customers.
    private static string Bubble(string text)
    {
        return "<b>" + text.ToUpperInvariant() + "</b>";
    }
}