using UnityEngine;
using UnityEngine.UI;

public class WorldStateManager : MonoBehaviour
{
    // UI reference that holds a slider (used as a "hype" or score bar)
    [SerializeField] private GameObject hypeBar;
    [SerializeField] private HypeBar hypeBarScript;

    // Reference to LightData script that controls lighting visuals
    private LightData lightData;
   


    private void Start()
    {
        hypeBar = GameObject.FindGameObjectWithTag("HypeBar");
        hypeBarScript = hypeBar.GetComponent<HypeBar>();
        lightData = GetComponent<LightData>();
    }

    private void Update()
    {
        // Continuously apply score-based lighting updates each frame
        ApplyScore();
    }
    void ApplyScore()
    {
        float score = hypeBarScript.value;    
        lightData.ApplyValues(score, score, score, score, score);
    }
}
