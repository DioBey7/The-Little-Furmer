using UnityEngine;
using UnityEngine.UI;

public class FactoryTrigger : MonoBehaviour, IInteractableArea
{
    [SerializeField] private FactoryArea targetFactory;
    [SerializeField] private Image fillCircleImage;

    private float currentFillTime = 0f;

    private float GetDuration() => LevelManager.Instance != null && LevelManager.Instance.CurrentLevelConfig != null ? LevelManager.Instance.CurrentLevelConfig.factorySettings.itemDepositAnimationTime : 0.2f;

    private void Start()
    {
        if (fillCircleImage != null) fillCircleImage.fillAmount = 0f;
    }

    public void OnPlayerEnter(ItemCollecting player) { ResetFill(); }
    public void OnPlayerExit(ItemCollecting player) { ResetFill(); }

    public void ProcessInteraction(float deltaTime, ItemCollecting player)
    {
        if (player == null || targetFactory == null)
        {
            ResetFill();
            return;
        }

        ItemType? typeToTake = null;
        foreach (var req in targetFactory.requiredIngredients)
        {
            if (player.HasItem(req.itemType) && targetFactory.NeedsItem(req.itemType))
            {
                typeToTake = req.itemType;
                break;
            }
        }

        if (typeToTake.HasValue)
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

                targetFactory.NotifyItemIncoming(typeToTake.Value);
                player.ExecuteFactoryUnloadSignal(targetFactory, targetFactory.inputPoint, typeToTake.Value);
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
