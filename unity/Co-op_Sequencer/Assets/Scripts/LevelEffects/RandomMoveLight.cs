using UnityEngine;

public class RandomMoveLight : MonoBehaviour
{
    [Header("Y Sweep Limits")]
    public float yMinAngle = -60f;
    public float yMaxAngle = 60f;
    public float yMinSpeed = 20f;
    public float yMaxSpeed = 60f;

    [Header("X Tilt Limits")]
    public float xMinAngle = -20f;
    public float xMaxAngle = 20f;
    public float xSpeed = 2f;

    [Header("Timing")]
    public float changeInterval = 2f;

    private float ySpeed;
    private float currentY;
    private float currentX;
    private float targetX;

    private float timer;

    void Start()
    {
        // Start roughly centered
        currentY = 0f;
        currentX = 0f;

        PickNewValues();
    }

    void Update()
    {
        // --- Y ROTATION (continuous sweep with limits) ---
        currentY += ySpeed * Time.deltaTime;

        // Clamp and bounce back if limits reached
        if (currentY > yMaxAngle)
        {
            currentY = yMaxAngle;
            ySpeed *= -1f;
        }
        else if (currentY < yMinAngle)
        {
            currentY = yMinAngle;
            ySpeed *= -1f;
        }

        // --- X TILT (smooth random target) ---
        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * xSpeed);

        // Apply rotation
        transform.localRotation = Quaternion.Euler(currentX, currentY, 0f);

        // --- RANDOM CHANGES OVER TIME ---
        timer += Time.deltaTime;
        if (timer >= changeInterval)
        {
            PickNewValues();
            timer = 0f;
        }
    }

    void PickNewValues()
    {
        // Random Y speed + direction
        float speed = Random.Range(yMinSpeed, yMaxSpeed);
        ySpeed = speed * (Random.value > 0.5f ? 1f : -1f);

        // Random X tilt target
        targetX = Random.Range(xMinAngle, xMaxAngle);
    }
}
