using UnityEngine;

/// <summary>
/// Level dressing (trees, flowers, rocks) is spawned with the level prefab, so build-time static
/// batching never sees it. Combining it once at start keeps the draw-call count low on WebGL.
/// </summary>
public class StaticDecor : MonoBehaviour
{
    private void Start()
    {
        StaticBatchingUtility.Combine(gameObject);
    }
}
