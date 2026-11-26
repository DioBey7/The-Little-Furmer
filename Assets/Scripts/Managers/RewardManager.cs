using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    [SerializeField] private Button rewardButton;
    [SerializeField] private float rewardInterval = 300f;

    private float timer = 0f;
    private bool isRewardReady = false;

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
        if (rewardButton != null)
        {
            rewardButton.gameObject.SetActive(false);
            rewardButton.onClick.AddListener(OnRewardButtonClicked);
        }

        timer = rewardInterval;
        StartCoroutine(RewardTimerRoutine());
    }

    private IEnumerator RewardTimerRoutine()
    {
        WaitForSeconds tick = new WaitForSeconds(1f);

        while (true)
        {
            if (GameManager.Instance != null && GameManager.Instance.isGameActive && !isRewardReady)
            {
                timer -= 1f;
                if (timer <= 0f)
                {
                    ShowRewardButton();
                }
            }
            yield return tick;
        }
    }

    private void ShowRewardButton()
    {
        isRewardReady = true;
        if (rewardButton != null)
        {
            rewardButton.gameObject.SetActive(true);
            // Pop in and keep wiggling gently so the player notices the gift.
            Transform t = rewardButton.transform;
            t.DOKill();
            t.localScale = Vector3.zero;
            DOTween.Sequence().SetTarget(t)
                .Append(t.DOScale(1f, 0.4f).SetEase(Ease.OutBack))
                .AppendCallback(() => t.DOPunchRotation(new Vector3(0f, 0f, 12f), 0.6f, 6, 0.5f).SetLoops(-1, LoopType.Restart).SetDelay(1.2f));
        }
    }

    private void OnRewardButtonClicked()
    {
        if (!isRewardReady) return;

        GiveReward();

        isRewardReady = false;
        if (rewardButton != null)
        {
            rewardButton.transform.DOKill();
            rewardButton.transform.localRotation = Quaternion.identity;
            rewardButton.transform.localScale = Vector3.one;
            rewardButton.gameObject.SetActive(false);
        }

        timer = rewardInterval;
    }

    public void GiveReward()
    {
        // Scale the gift with the level so it still matters when prices grow (~$275, ~$1.1K, ~$2.5K...).
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex + 1 : 1;
        int cashAmount = 5 * Random.Range(50, 61) * level * level;

        if (UiManager.Instance != null)
        {
            UiManager.Instance.AddCash(cashAmount);
            UiManager.Instance.AddNotification("+$" + UiManager.FormatMoney(cashAmount) + " REWARD!", Color.yellow);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayCashPickupSound();
        }
    }
}
