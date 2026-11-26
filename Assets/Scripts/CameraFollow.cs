using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 30f, -22f);
    [SerializeField] private float smoothSpeed = 10f;

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.Lerp(transform.position, target.position + offset, smoothSpeed * Time.deltaTime);
    }
}
