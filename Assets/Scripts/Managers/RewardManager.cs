using System.Collections;
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
        }
    }

    private void OnRewardButtonClicked()
    {
        if (!isRewardReady) return;

        GiveReward();

        isRewardReady = false;
        if (rewardButton != null)
        {
            rewardButton.gameObject.SetActive(false);
        }

        timer = rewardInterval;
    }

    public void GiveReward()
    {
        int cashAmount = 5 * Random.Range(50, 61);

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
