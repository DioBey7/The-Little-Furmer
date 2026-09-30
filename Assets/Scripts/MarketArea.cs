using System.Collections.Generic;
using UnityEngine;

public class MarketArea : MonoBehaviour, IInteractableArea
{
    public List<Transform> queuePoints = new List<Transform>();
    public List<CustomerAI> customerQueue = new List<CustomerAI>();

    private float deliveryTimer = 0f;
    private float cachedInterval = 0.5f;

    private void Start()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null)
        {
            cachedInterval = LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.itemTransferDelayToCustomer;
        }
    }

    public void OnPlayerEnter(ItemCollecting player)
    {
        deliveryTimer = 0f;
    }

    public void OnPlayerExit(ItemCollecting player)
    {
        deliveryTimer = 0f;
    }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (customerQueue.Count == 0 || player == null) return;

        deliveryTimer += deltaTime;
        if (deliveryTimer >= cachedInterval)
        {
            deliveryTimer = 0f;
            TryDeliverItemToFirstCustomer(player);
        }
    }

    private void TryDeliverItemToFirstCustomer(ItemCollecting playerCollector)
    {
        CustomerAI firstCustomer = customerQueue[0];

        if (firstCustomer == null || firstCustomer.currentState == CustomerAI.CustomerState.Leaving)
        {
            if (firstCustomer == null)
            {
                customerQueue.RemoveAt(0);
                UpdateQueuePositions();
            }
            return;
        }

        if (firstCustomer.currentState == CustomerAI.CustomerState.WalkingToQueue) return;

        for (int i = 0; i < firstCustomer.requestedItems.Count; i++)
        {
            var req = firstCustomer.requestedItems[i];
            if (req.amount > 0)
            {
                if (playerCollector.HasItem(req.itemType))
                {
                    playerCollector.ExecuteCustomerUnloadSignal(firstCustomer, firstCustomer.transform, req.itemType);
                    return;
                }
            }
        }
    }

    public void AddCustomer(CustomerAI customer)
    {
        if (customer != null && !customerQueue.Contains(customer))
        {
            customerQueue.Add(customer);
        }
    }

    public void RemoveCustomerFromQueue(CustomerAI customer)
    {
        if (customerQueue.Remove(customer))
        {
            UpdateQueuePositions();
        }
    }

    private void UpdateQueuePositions()
    {
        if (queuePoints == null || queuePoints.Count == 0) return;

        for (int i = 0; i < customerQueue.Count; i++)
        {
            CustomerAI customer = customerQueue[i];
            if (customer != null)
            {
                int pointIndex = Mathf.Min(i, queuePoints.Count - 1);
                Transform targetPoint = queuePoints[pointIndex];
                customer.UpdateQueuePosition(i, targetPoint);
            }
        }
    }
}
