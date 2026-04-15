using UnityEngine;

/// <summary>
/// Drives the HypeBar based on timing scores.
/// Works with 3D meshes (no Unity UI Slider needed).
/// </summary>
public class HypeBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SymbolScroller symbolScroller;
    [SerializeField] private Renderer barRenderer;
    [SerializeField] private Animator animator;

    [Header("Shader Settings")]
    private static readonly int fillPropertyID = Shader.PropertyToID("_Fill");

    [Header("Settings")]
    [SerializeField] private float fillPerPerfect = 0.10f;
    [SerializeField] private float drainOnMiss = 0.08f;
    [SerializeField] private float drainOnWrongInput = 0.05f;
    [SerializeField] private float passiveDrain = 0.01f;
    [SerializeField] private int maxPoints = 100;

    private float value = 0.5f; // 0–1

    void OnEnable()
    {
        if (symbolScroller != null)
        {
            symbolScroller.OnTimingScored += HandleTimingScored;
            symbolScroller.OnWrongInput += HandleWrongInput;
        }
    }
    void Start()
    {
        barRenderer = GetComponent<Renderer>();
        
    }
    void OnDisable()
    {
        if (symbolScroller != null)
        {
            symbolScroller.OnTimingScored -= HandleTimingScored;
            symbolScroller.OnWrongInput -= HandleWrongInput;
        }
    }

    void Update()
    {
        // Passive drain
        value = Mathf.Max(0f, value - passiveDrain * Time.deltaTime);
        UpdateVisuals();
    }

    private void HandleTimingScored(int points)
    {
        if (points <= 0)
        {
            value = Mathf.Max(0f, value - drainOnMiss);
        }
        else
        {
            float fill = fillPerPerfect * ((float)points / maxPoints);
            value = Mathf.Min(1f, value + fill);
        }

        UpdateVisuals();
    }

    private void HandleWrongInput()
    {
        value = Mathf.Max(0f, value - drainOnWrongInput);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Update shader
        if (barRenderer != null)
        {
            var mats = barRenderer.materials;
            mats[1].SetFloat(fillPropertyID, value);
            barRenderer.materials = mats;
        }
    }

    public float HypeLevel => value;
}
