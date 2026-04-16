using UnityEditor.SpeedTree.Importer;
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
    [SerializeField] private HypeBarAnimationController hypeBarAnimationController;
    [SerializeField] private Material scrollMaterial;

    [Header("Shader Settings")]
    private static readonly int fillPropertyID = Shader.PropertyToID("_Fill");
    private static readonly int fullBar = Shader.PropertyToID("_Full");
    private static readonly int fullBarEffect = Shader.PropertyToID("_HYPEBARFULL");
    private Material[] mats;
    private MaterialPropertyBlock block;

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
        hypeBarAnimationController = GetComponent<HypeBarAnimationController>();

        mats = barRenderer.materials;
        block = new MaterialPropertyBlock();
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

        if(Input.GetKeyDown(KeyCode.Space))
        {
            HandleTimingScored(100); // Simulate a perfect hit for testing
        }
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
            hypeBarAnimationController.Hit();
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
        bool isFull = value >= 0.8f;

        barRenderer.GetPropertyBlock(block);

        block.SetFloat(fillPropertyID, value);
        block.SetFloat(fullBar, isFull ? 1f : 0f);

        barRenderer.SetPropertyBlock(block);

        if (isFull)
        {
            hypeBarAnimationController.HypeBarExplosion();
            barRenderer.material.EnableKeyword("_HYPEBARFULL");
        }
        else
        {
            hypeBarAnimationController.ResetHypeBar();
            barRenderer.material.DisableKeyword("_HYPEBARFULL");
        }
    }


    public float HypeLevel => value;
}
