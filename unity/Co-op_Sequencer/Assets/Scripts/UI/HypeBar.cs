using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the HypeBar slider based on timing scores from SymbolScroller.
/// Perfect hits fill the bar, misses drain it.
///
/// Setup: attach to the HypeBar GameObject (which has a Slider component)
/// and assign the SymbolScroller reference in the Inspector.
/// </summary>
[RequireComponent(typeof(Slider))]
public class HypeBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SymbolScroller symbolScroller;

    [Header("Settings")]
    [Tooltip("How much a perfect hit (100 pts) fills the bar (0-1 scale).")]
    [SerializeField] private float fillPerPerfect = 0.10f;

    [Tooltip("How much the bar drains on a miss.")]
    [SerializeField] private float drainOnMiss = 0.08f;

    [Tooltip("Passive drain per second to keep pressure on the player.")]
    [SerializeField] private float passiveDrain = 0.01f;

    [Tooltip("Max points value used to normalize incoming scores.")]
    [SerializeField] private int maxPoints = 100;

    private Slider _slider;

    void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.value    = 0.5f; // start half-full
    }

    void OnEnable()
    {
        if (symbolScroller != null)
            symbolScroller.OnTimingScored += HandleTimingScored;
    }

    void OnDisable()
    {
        if (symbolScroller != null)
            symbolScroller.OnTimingScored -= HandleTimingScored;
    }

    void Update()
    {
        // Passive drain keeps pressure on the player
        _slider.value = Mathf.Max(0f, _slider.value - passiveDrain * Time.deltaTime);
    }

    private void HandleTimingScored(int points)
    {
        if (points <= 0)
        {
            // Miss — drain the bar
            _slider.value = Mathf.Max(0f, _slider.value - drainOnMiss);
        }
        else
        {
            // Hit — fill proportional to score (perfect=100 → full fillPerPerfect)
            float fill = fillPerPerfect * ((float)points / maxPoints);
            _slider.value = Mathf.Min(1f, _slider.value + fill);
        }
    }

    /// <summary>Current hype level 0-1.</summary>
    public float HypeLevel => _slider != null ? _slider.value : 0f;
}
