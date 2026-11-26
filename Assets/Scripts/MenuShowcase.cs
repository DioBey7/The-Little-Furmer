using DG.Tweening;
using UnityEngine;

/// <summary>
/// Main menu "showcase" camera: a slow orbit close to the player while the menu is up
/// (the game is paused, so it runs on unscaled time), then a smooth glide into the
/// normal follow view when Play is pressed.
/// </summary>
public class MenuShowcase : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private CameraFollow follow;
    [SerializeField] private Transform player;
    [SerializeField] private Animator playerAnimator;

    [Header("Orbit")]
    [SerializeField] private float distance = 6.6f;
    [SerializeField] private float height = 2.4f;
    [SerializeField] private float lookHeight = 0.75f;
    [SerializeField] private float degreesPerSecond = 9f;
    [SerializeField] private float startAngle = 200f;
    [Tooltip("Shifts the framing so the character sits right of centre, leaving room for the title and buttons.")]
    [SerializeField] private float sideOffset = -1.0f;

    private Quaternion gameRotation;
    private bool showing;
    private float angle;
    private AnimatorUpdateMode originalUpdateMode;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (follow == null && cam != null) follow = cam.GetComponent<CameraFollow>();
        if (cam != null) gameRotation = cam.transform.rotation;
    }

    private void Start()
    {
        Begin();
    }

    public void Begin()
    {
        if (cam == null || player == null) return;
        showing = true;
        angle = startAngle;
        if (follow != null) follow.enabled = false;
        if (playerAnimator != null)
        {
            originalUpdateMode = playerAnimator.updateMode;
            playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; // idle keeps playing while the game is paused
        }
        Place();
    }

    private void LateUpdate()
    {
        if (!showing) return;
        angle += degreesPerSecond * Time.unscaledDeltaTime;
        Place();
    }

    private void Place()
    {
        Vector3 focus = player.position + Vector3.up * lookHeight;
        Quaternion orbit = Quaternion.Euler(0f, angle, 0f);
        Vector3 pos = focus + orbit * new Vector3(0f, 0f, -distance) + Vector3.up * (height - lookHeight);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(focus - pos, Vector3.up);
        // Slide the view sideways so the character sits to the right of the menu.
        cam.transform.position += cam.transform.right * sideOffset;
    }

    /// <summary>Glide from the showcase view into the gameplay camera, then hand back to CameraFollow.</summary>
    public void End(float duration = 1.1f)
    {
        if (!showing) return;
        showing = false;
        if (playerAnimator != null) playerAnimator.updateMode = originalUpdateMode;
        if (cam == null || follow == null) return;

        Vector3 target = follow.Target != null ? follow.Target.position + follow.Offset : cam.transform.position;
        cam.transform.DOKill();
        DOTween.Sequence().SetUpdate(true).SetTarget(cam.transform)
            .Append(cam.transform.DOMove(target, duration).SetEase(Ease.InOutCubic))
            .Join(cam.transform.DORotateQuaternion(gameRotation, duration).SetEase(Ease.InOutCubic))
            .OnComplete(() => { if (follow != null) follow.enabled = true; });
    }
}
