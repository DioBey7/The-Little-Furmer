using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemData data;
    public bool isCollected = false;

    public ItemType ItemType => data.itemType;
    public GameObject ItemPrefab => data.itemPrefab;

    public Transform OriginDirtPoint { get; set; }
    public FactoryArea OriginFactory { get; set; }
    public Transform OriginFactoryPoint { get; set; }

    private Collider[] cachedColliders;

    private void Awake()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    public void SetCollidersState(bool state)
    {
        if (cachedColliders == null) return;
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null) cachedColliders[i].enabled = state;
        }
    }
}
