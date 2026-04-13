using UnityEngine;

public class BPMStageLight : MonoBehaviour
{
    [Header("BPM Settings")]
    public float bpm = 120f;

    [Header("Y Rotation Limits")]
    public float yMinAngle = -60f;
    public float yMaxAngle = 60f;

    [Header("X Tilt Limits")]
    public float xMinAngle = -20f;
    public float xMaxAngle = 20f;

    private float beatInterval;
    private float timer;
    private int beatCount = 0;

    void Start()
    {
        beatInterval = 60f / bpm;
        TriggerBeat(); // initial position
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= beatInterval)
        {
            timer -= beatInterval;
            beatCount = (beatCount + 1) % 4; // 4/4 loop
            TriggerBeat();
        }
    }

    void TriggerBeat()
    {
        float newY = 0f;
        float newX = 0f;

        switch (beatCount)
        {
            case 0: // Beat 1 (strong hit)
                newY = Random.Range(yMinAngle, yMaxAngle);
                newX = Random.Range(xMinAngle, xMaxAngle);
                break;

            case 1: // Beat 2
                newX = Random.Range(xMinAngle, xMaxAngle);
                newY = transform.localEulerAngles.y; // keep Y
                break;

            case 2: // Beat 3
                newY = Random.Range(yMinAngle, yMaxAngle);
                newX = transform.localEulerAngles.x; // keep X
                break;

            case 3: // Beat 4
                newX = Random.Range(xMinAngle, xMaxAngle);
                newY = transform.localEulerAngles.y;
                break;
        }

        // Apply instantly (no smoothing, no in-between motion)
        transform.localRotation = Quaternion.Euler(newX, newY, 0f);
    }
}
