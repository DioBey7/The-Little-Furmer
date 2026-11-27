using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Keeps the objectives card out of the way: it opens in full when a level starts, then folds
// down to a single line (the next goal). Tapping the card opens it again for a few seconds.
public class ObjectiveCollapse : MonoBehaviour, IPointerClickHandler
{
    public Image header;
    public TMP_Text headerText;
    public RectTransform rows;
    public float openSeconds = 6f;

    private bool collapsed;
    private float collapseAt;
    private int lastRowCount = -1;
    private float rowsOpenY;

    private void Awake()
    {
        if (rows != null) rowsOpenY = rows.anchoredPosition.y;
        // The rows sit outside the header rect, so give them a click target too.
        if (rows != null && rows.GetComponent<Graphic>() == null)
        {
            var img = rows.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
        }
        if (rows != null && rows.GetComponent<ObjectiveCollapseRelay>() == null)
            rows.gameObject.AddComponent<ObjectiveCollapseRelay>().owner = this;
    }

    private void OnEnable() => Open();

    public void Open()
    {
        collapsed = false;
        collapseAt = Time.unscaledTime + openSeconds;
        Apply();
    }

    public void OnPointerClick(PointerEventData eventData) => Toggle();

    public void Toggle()
    {
        if (collapsed) Open();
        else { collapsed = true; Apply(); }
        if (SoundManager.Instance != null) SoundManager.Instance.PlayUIClickSound();
    }

    private void Update()
    {
        if (rows == null) return;
        int count = rows.childCount;
        if (count > lastRowCount && lastRowCount >= 0 && count > 1) Open(); // a new level's goals arrived
        lastRowCount = count;
        if (!collapsed && Time.unscaledTime >= collapseAt) collapsed = true;
        Apply();
    }

    private void Apply()
    {
        if (rows == null) return;
        if (header != null) header.enabled = !collapsed;
        if (headerText != null) headerText.enabled = !collapsed;
        var p = rows.anchoredPosition;
        float y = collapsed ? rowsOpenY + (header != null ? header.rectTransform.rect.height + 4f : 0f) : rowsOpenY;
        if (!Mathf.Approximately(p.y, y)) rows.anchoredPosition = new Vector2(p.x, y);

        bool shownOne = false;
        for (int i = 0; i < rows.childCount; i++)
        {
            var child = rows.GetChild(i);
            bool finishing = child.localScale.x < 0.99f && child.gameObject.activeSelf && shownOne;
            bool show = !collapsed || (!shownOne);
            if (child.gameObject.activeSelf != show && !finishing) child.gameObject.SetActive(show);
            if (show) shownOne = true;
        }
    }
}

// Forwards taps on the objective rows to the card.
public class ObjectiveCollapseRelay : MonoBehaviour, IPointerClickHandler
{
    public ObjectiveCollapse owner;
    public void OnPointerClick(PointerEventData eventData) { if (owner != null) owner.Toggle(); }
}
