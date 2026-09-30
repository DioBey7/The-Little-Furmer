using UnityEngine;

public class PlayerMagnet : MonoBehaviour
{
    [SerializeField] private Transform collectionTarget;

    private void Awake()
    {
        if (collectionTarget == null)
        {
            collectionTarget = transform.parent != null ? transform.parent : transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out CashItem cash))
        {
            cash.MagnetToPlayer(collectionTarget);
        }
    }
}
