using UnityEngine;
using UnityEngine.UI;

public class StockPointReference : MonoBehaviour, IInteractableArea
{
    [SerializeField] private StockArea targetStockArea;
    [SerializeField] private Image fillCircleImage;

    private float currentFillTime = 0f;

    private float GetDuration() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.stockAndMarketSettings.itemTransferDelayToStock : 1f;

    private void Start()
    {
        if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;
    }

    public void OnPlayerEnter(ItemCollecting player)
    {
        if (targetStockArea != null) targetStockArea.RefreshVisuals();
        ResetFill();
    }

    public void OnPlayerExit(ItemCollecting player) { ResetFill(); }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (player == null || targetStockArea == null || !targetStockArea.isTypeCached)
        {
            ResetFill();
            return;
        }

        if (player.HasItem(targetStockArea.AcceptedItemType) && targetStockArea.HasEmptyStockPoint())
        {
            currentFillTime += deltaTime;
            float dur = GetDuration();
            float progress = Mathf.Clamp01(currentFillTime / dur);

            if (fillCircleImage != null)
            {
                fillCircleImage.fillAmount = progress;
            }

            if (currentFillTime >= dur)
            {
                currentFillTime = 0f;
                if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;

                Transform targetStockPoint = targetStockArea.GetNextStockPoint();
                if (targetStockPoint != null)
                {
                    player.ExecuteSpecificUnloadSignal(targetStockArea, targetStockPoint, targetStockArea.AcceptedItemType);
                }
            }
        }
        else
        {
            ResetFill();
        }
    }

    private void ResetFill()
    {
        currentFillTime = 0f;
        if (fillCircleImage != null)
        {
            fillCircleImage.fillAmount = 0f;
        }
    }
}
