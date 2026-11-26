using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a chunky button feel pressed: while the pointer is held down, every child
/// (label, icon) sinks by the same amount as the button's pressed sprite, so the
/// contents move together with the button face.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    [Tooltip("How far the contents sink, as a fraction of the button height (matches the pack's pressed sprites).")]
    [SerializeField] private float sinkFraction = 0.11f;

    private Selectable selectable;
    private RectTransform rect;
    private RectTransform[] children;
    private Vector2[] restPositions;
    private bool held;
    private bool pressed;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        rect = (RectTransform)transform;
        CacheChildren();
    }

    private void CacheChildren()
    {
        int n = transform.childCount;
        children = new RectTransform[n];
        restPositions = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            children[i] = transform.GetChild(i) as RectTransform;
            if (children[i] != null) restPositions[i] = children[i].anchoredPosition;
        }
    }

    private void OnDisable()
    {
        held = false;
        SetPressed(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (selectable != null && !selectable.IsInteractable()) return;
        held = true;
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        held = false;
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData) => SetPressed(false);

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (held) SetPressed(true);
    }

    private void SetPressed(bool value)
    {
        if (pressed == value || children == null) return;
        if (value && children.Length != transform.childCount) CacheChildren();
        pressed = value;
        float sink = rect.rect.height * sinkFraction;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null) continue;
            children[i].anchoredPosition = value ? restPositions[i] - new Vector2(0f, sink) : restPositions[i];
        }
    }
}
