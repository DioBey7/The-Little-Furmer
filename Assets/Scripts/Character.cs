using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    public void ClearInteractables()
    {
        interactables.Clear();
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
                // Areas that were destroyed or switched off (e.g. an unlock area after purchase)
                // never send OnTriggerExit, so drop them here instead of calling into them.
                if (interactables[i] is Behaviour area && (area == null || !area.isActiveAndEnabled))
                {
                    interactables.RemoveAt(i);
                    continue;
                }
                interactables[i].ProcessInteraction(Time.deltaTime, itemCollecting);
            }
        }
    }

    private void GetInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startInputPos = Input.mousePosition;
            isMoving = true;
        }
        else if (Input.GetMouseButton(0))
        {
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
        }
        else if (Input.GetMouseButtonUp(0))
        {
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
