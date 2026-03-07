using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    // distance behind the target in its forward direction
    public float distance = 6f;
    public float height = 3f;
    public float smoothTime = 0.1f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null)
            return;

        // calculate desired position strictly behind the target
        Vector3 desiredPos = target.position - target.forward * distance + Vector3.up * height;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);

        // always look at the target (slightly above its position)
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}