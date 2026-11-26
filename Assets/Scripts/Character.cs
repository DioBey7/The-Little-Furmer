using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class Character : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float detectionRadius = 0.75f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, 0);
    [SerializeField] private Animator animator;

    private float deadzoneThreshold = 15f;
    private NavMeshAgent agent;
    private Vector2 startInputPos;
    private Vector2 currentInputPos;
    private bool isMoving;
    private bool isDragging;
    private Vector3 moveDirection;
    private LayerMask borderLayer;

    private ItemCollecting itemCollecting;
    private List<IInteractableArea> interactables = new List<IInteractableArea>();

    private void Start()
    {
        borderLayer = LayerMask.GetMask("Border");
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.updateRotation = false;

        itemCollecting = GetComponent<ItemCollecting>();

        if (agent != null && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Cash"))
        {
            if (itemCollecting != null) itemCollecting.CollectCash(other.gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & borderLayer) != 0)
        {
            isMoving = false;
            moveDirection = Vector3.zero;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            return;
        }

        if (other.TryGetComponent(out Item itemComp))
        {
            if (!itemComp.isCollected && other.gameObject.activeSelf)
            {
                if (itemCollecting != null) itemCollecting.CollectItem(other.gameObject, itemComp);
            }
            return;
        }

        if (other.TryGetComponent(out IInteractableArea interactable) || (interactable = other.GetComponentInParent<IInteractableArea>()) != null)
        {
            if (!interactables.Contains(interactable))
            {
                interactables.Add(interactable);
                if (itemCollecting != null) interactable.OnPlayerEnter(itemCollecting);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Cash") || other.TryGetComponent(out Item _)) return;

        if (other.TryGetComponent(out IInteractableArea interactable) || (interactable = other.GetComponentInParent<IInteractableArea>()) != null)
        {
            if (interactables.Contains(interactable))
            {
                if (itemCollecting != null) interactable.OnPlayerExit(itemCollecting);
                interactables.Remove(interactable);
            }
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.isGameActive)
        {
            isMoving = false;
            moveDirection = Vector3.zero;
            UpdateAnimation();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }
        else if (agent != null && agent.isOnNavMesh && agent.isStopped)
        {
            agent.isStopped = false;
        }

        GetInput();
        MoveAndRotate();
        UpdateAnimation();

        if (itemCollecting != null)
        {
            for (int i = interactables.Count - 1; i >= 0; i--)
            {
                interactables[i].ProcessInteraction(Time.deltaTime, itemCollecting);
            }
        }
    }

    private void GetInput()
    {
        // Pointer drag (touch on phones, mouse on desktop browsers). Mouse input also
        // covers touch, because Unity simulates the mouse from the first touch.
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            startInputPos = Input.mousePosition;
            isDragging = true;
        }
        if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            isMoving = true;
            currentInputPos = Input.mousePosition;
            Vector2 delta = currentInputPos - startInputPos;

            if (delta.sqrMagnitude < (deadzoneThreshold * deadzoneThreshold))
            {
                moveDirection = Vector3.zero;
            }
            else
            {
                Vector2 normalizedDelta = delta.normalized;
                moveDirection = new Vector3(normalizedDelta.x, 0f, normalizedDelta.y);
            }
            return;
        }

        // Keyboard (WASD / arrow keys) for desktop browsers.
        Vector2 keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (keys.sqrMagnitude > 0.01f)
        {
            isMoving = true;
            keys = keys.normalized;
            moveDirection = new Vector3(keys.x, 0f, keys.y);
            return;
        }

        isMoving = false;
        moveDirection = Vector3.zero;
    }

    // A press that starts on a button or panel should not also move the character.
    // Only screen-space UI counts: the world-space price/progress canvases on the
    // ground also have raycasters and must not block walking.
    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    private static bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        Vector2 pressPosition = Input.mousePosition;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began) { pressPosition = touch.position; break; }
        }

        PointerEventData pointer = new PointerEventData(eventSystem) { position = pressPosition };
        uiHits.Clear();
        eventSystem.RaycastAll(pointer, uiHits);
        foreach (RaycastResult hit in uiHits)
        {
            Canvas canvas = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.WorldSpace) return true;
        }
        return false;
    }

    // Releasing the mouse outside the browser window never sends a button-up event.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            isDragging = false;
            isMoving = false;
            moveDirection = Vector3.zero;
        }
    }

    private void MoveAndRotate()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        if (!isMoving || moveDirection.sqrMagnitude < 0.001f)
        {
            agent.velocity = Vector3.zero;
            return;
        }

        Vector3 desiredVelocity = moveDirection * moveSpeed;

        if (Physics.SphereCast(transform.position + (Vector3.up * 0.5f), 0.3f, moveDirection, out RaycastHit hit, 0.5f, borderLayer))
        {
            desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, hit.normal);

            if (desiredVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion slideRotation = Quaternion.LookRotation(desiredVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, slideRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        agent.velocity = desiredVelocity;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (isMoving && moveDirection.sqrMagnitude >= 0.001f)
        {
            animator.SetBool("IsMoving", true);
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + offset, detectionRadius);
    }
}
