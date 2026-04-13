using UnityEngine;
using UnityEngine.UI;

public class WorldStateManager : MonoBehaviour
{
    // UI reference that holds a slider (used as a "hype" or score bar)
    [SerializeField] private GameObject hypeBar;

    // Reference to LightData script that controls lighting visuals
    private LightData lightData;

    // Slider component extracted from hypeBar
    private Slider score;

    private void Start()
    {
       
        score = hypeBar.GetComponent<Slider>();
        lightData = GetComponent<LightData>();
    }

    private void Update()
    {
        // Continuously apply score-based lighting updates each frame
        ApplyScore();
    }
    void ApplyScore()
    {
        float value = score.value;

        // Pass same value into all lighting parameters for now
        lightData.ApplyValues(value, value, value, value, value);
    }
}
