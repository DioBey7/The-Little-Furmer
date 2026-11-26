using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small idle motion for a locked area pad so it reads as something to walk onto:
/// the lock bobs, the price pill breathes, and the pad swells while the player stands on it.
/// Purely visual; UnlockArea keeps all the logic.
/// </summary>
[RequireComponent(typeof(UnlockArea))]
public class LockedPadIdle : MonoBehaviour
{
    [SerializeField] private float bobHeight = 6f;
    [SerializeField] private float bobSpeed = 2.2f;
    [SerializeField] private float pulseAmount = 0.06f;

    private UnlockArea area;
    private RectTransform lockRect;
    private Transform pricePill;
    private Transform frame;
    private Vector2 lockHome;
    private float phase;

    private void Awake()
    {
        area = GetComponent<UnlockArea>();
        lockRect = area.lockIcon as RectTransform;
        if (lockRect != null) lockHome = lockRect.anchoredPosition;
        if (area.costText != null) pricePill = area.costText.transform.parent;
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null) frame = canvas.transform.Find("PadFrame");
        phase = Random.value * Mathf.PI * 2f; // pads next to each other don't move in sync
    }

    private System.Collections.IEnumerator Start()
    {
        // UnlockArea writes a two-size rich-text name in its Start; on the ribbon one plain line reads better.
        yield return null;
        if (area.areaNameText != null && area.areaNameText.transform.parent != null && area.areaNameText.transform.parent.name == "NameRibbon")
        {
            string name = string.IsNullOrEmpty(area.customAreaName) ? "AREA" : area.customAreaName;
            area.areaNameText.text = (name + " " + area.areaType).ToUpperInvariant();
        }
    }

    private void Update()
    {
        float t = Time.time * bobSpeed + phase;

        if (lockRect != null)
            lockRect.anchoredPosition = lockHome + new Vector2(0f, Mathf.Sin(t) * bobHeight);

        if (pricePill != null && pricePill != transform)
            pricePill.localScale = Vector3.one * (1f + Mathf.Sin(t * 1.3f) * pulseAmount * 0.5f);

        if (frame != null)
        {
            bool active = area.fillCircleImage != null && area.fillCircleImage.fillAmount > 0f;
            float target = active ? 1.08f : 1f;
            frame.localScale = Vector3.Lerp(frame.localScale, Vector3.one * target, Time.deltaTime * 10f);
        }
    }
}
