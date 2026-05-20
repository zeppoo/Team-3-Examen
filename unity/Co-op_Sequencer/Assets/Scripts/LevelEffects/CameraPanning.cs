using UnityEngine;

public class PanBetweenPointsCamera : MonoBehaviour
{
    [Header("Focus Target")]
    public Transform focusTarget; // Object the camera looks at

    [Header("Movement Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public bool loop = true;

    private float t = 0f;
    private bool movingForward = true;

    void Update()
    {
        if (focusTarget == null || pointA == null || pointB == null)
            return;

        // Move t value
        if (movingForward)
            t += moveSpeed * Time.deltaTime;
        else
            t -= moveSpeed * Time.deltaTime;

        // Clamp movement
        t = Mathf.Clamp01(t);

        // Move camera between points
        transform.position = Vector3.Lerp(
            pointA.position,
            pointB.position,
            t
        );

        // Always look at focus object
        transform.LookAt(focusTarget);

        // Reverse direction at ends
        if (loop)
        {
            if (t >= 1f)
                movingForward = false;

            if (t <= 0f)
                movingForward = true;
        }
    }
}
