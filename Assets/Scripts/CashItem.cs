using DG.Tweening;
using UnityEngine;

public class CashItem : MonoBehaviour
{
    private int cashValue;
    private bool isCollected = false;
    private Collider cashCollider;
    private Rigidbody rb;
    private float spawnTime;

    public int CashValue => cashValue;

    private void Awake()
    {
        cashCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(int value)
    {
        cashValue = value;
        isCollected = false;
        spawnTime = Time.time;

        if (cashCollider != null) cashCollider.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void MagnetToPlayer(Transform playerTarget)
    {
        if (isCollected) return;

        float timeAlive = Time.time - spawnTime;
        if (timeAlive < 0.6f)
        {
            float waitTime = 0.6f - timeAlive;
            DOVirtual.DelayedCall(waitTime, () =>
            {
                if (this != null && gameObject.activeInHierarchy && !isCollected)
                {
                    StartMagnetTween(playerTarget);
                }
            });
            return;
        }

        StartMagnetTween(playerTarget);
    }

    private void StartMagnetTween(Transform playerTarget)
    {
        if (isCollected || playerTarget == null) return;
        isCollected = true;

        gameObject.tag = "Untagged";

        if (cashCollider != null) cashCollider.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
        }

        transform.DOKill();

        transform.DOMoveY(transform.position.y + 1.2f, 0.15f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            float duration = Random.Range(0.2f, 0.35f);
            Vector3 startPos = transform.position;

            DOVirtual.Float(0f, 1f, duration, (t) =>
            {
                if (playerTarget != null)
                {
                    Vector3 targetPos = playerTarget.position + (Vector3.up * 1f);
                    transform.position = Vector3.Lerp(startPos, targetPos, t * t);
                }
            }).OnComplete(() =>
            {
                if (UiManager.Instance != null) UiManager.Instance.AddCash(cashValue);
                if (SoundManager.Instance != null) SoundManager.Instance.PlayCashPickupSound();

                if (CashManager.Instance != null)
                {
                    CashManager.Instance.ReturnToPool(gameObject);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            });
        });
    }
}
