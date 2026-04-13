using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a queue of upcoming symbols with the active one in a highlight box.
/// Players must perform the correct action within a time window.
/// A rotating timing indicator orbits the active frame — when the player hits,
/// the indicator freezes and points are awarded based on how close to the
/// "perfect" moment (center of the beat) the input was.
///
/// Setup:
///   - activeSlot       : RectTransform where the current symbol is shown (the "box")
///   - queueParent      : RectTransform that holds the upcoming symbol tiles (vertical list)
///   - symbolPrefab     : prefab with an Image component
///   - gameManager      : reference to GameManager
///   - visibleQueueSize : how many upcoming symbols to show below the active box
/// </summary>
public class SymbolScroller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager  gameManager;
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private GameObject   symbolPrefab;

    [Header("UI Slots")]
    [SerializeField] private RectTransform activeSlot;   // the box where the current symbol lives
    [SerializeField] private RectTransform queueParent;  // horizontal list of upcoming symbols (use Horizontal Layout Group)

    [Header("Settings")]
    [SerializeField] private int   visibleQueueSize = 5; // how many upcoming symbols to show
    [SerializeField] private float beatInterval = 1f;     // seconds per symbol (placeholder until BPM is wired)
    public float BeatInterval { get => beatInterval; set => beatInterval = Mathf.Max(0.2f, value); }

    [Header("Scratch")]
    [SerializeField] private float scratchVelocityThreshold = 3f;

    [Header("Timing Indicator")]
    [SerializeField] private Color indicatorColor = Color.white;
    [SerializeField] private float indicatorSize  = 16f;  // px diameter of the dot
    [SerializeField] private float indicatorOrbitPadding = 8f; // extra px outside the active slot

    [Header("Scoring")]
    [SerializeField] private int perfectPoints = 100;
    [SerializeField] private int goodPoints    = 50;
    [SerializeField] private int okPoints      = 25;
    [Tooltip("Fraction of beat considered 'perfect' (0-0.5). E.g. 0.08 = ±8% of beat around center.")]
    [SerializeField] private float perfectWindow = 0.08f;
    [Tooltip("Fraction considered 'good' (0-0.5).")]
    [SerializeField] private float goodWindow    = 0.20f;

    /// <summary>Fired when the active symbol is resolved (hit or missed). Bool = was it a hit.</summary>
    public event Action<SymbolInstance, bool> OnSymbolResolved;

    /// <summary>Fired on a successful hit with the points earned (0 for miss).</summary>
    public event Action<int> OnTimingScored;

    /// <summary>Fired when the correct player presses the wrong button/scratch direction.</summary>
    public event Action OnWrongInput;

    /// <summary>Fired when the entire sequence has been completed.</summary>
    public event Action OnSequenceComplete;

    // State
    private SymbolInstance[] _sequence;
    private int   _currentIndex = 0;
    private float _timer        = 0f;
    private bool  _running      = false;
    private bool  _paused       = false;
    private bool  _activeHit    = false;

    // Cooldown after a hit — blocks input for a short period so queued scratch
    // events don't instantly consume the next symbol.
    private float _hitCooldown  = 0f;
    private const float HIT_COOLDOWN_DURATION = 0.15f; // seconds

    // Active symbol UI
    private GameObject _activeGO;
    private Image      _activeImg;

    // Timing indicator (created at runtime)
    private GameObject _indicatorGO;
    private RectTransform _indicatorRT;
    private Image _indicatorImg;
    private bool _indicatorFrozen = false;

    // Queue tiles
    private readonly List<(GameObject go, Image img)> _queueTiles = new();

    // ── Public API ────────────────────────────────────────────────────────

    void Awake()
    {
        if (lobbyManager == null)
        {
            foreach (var lm in FindObjectsByType<LobbyManager>(FindObjectsSortMode.None))
            {
                if (lm.Lobby != null) { lobbyManager = lm; break; }
            }
        }
    }

    public void StartScrolling()
    {
        var round = gameManager.CurrentRound;
        if (round == null || round.sequence == null || round.sequence.Length == 0)
        {
            Debug.LogWarning("[SymbolScroller] No sequence to play.");
            return;
        }

        _sequence     = round.sequence;
        _currentIndex = 0;
        _running      = true;
        _activeHit    = false;

        Debug.Log($"[SymbolScroller] Starting with {_sequence.Length} symbols, {beatInterval}s per beat");

        _hitCooldown     = 0f;
        _indicatorFrozen = false;

        ClearAll();
        ShowActive();
        CreateIndicator();
        RefreshQueue();
    }

    public void StopScrolling()
    {
        _running = false;
        ClearAll();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
    }

    // ── Input ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        InputReceiver.OnButtonInput  += HandleButtonInput;
        InputReceiver.OnScratchInput += HandleScratchInput;
    }

    void OnDisable()
    {
        InputReceiver.OnButtonInput  -= HandleButtonInput;
        InputReceiver.OnScratchInput -= HandleScratchInput;
    }

    private Player ResolvePlayer(string playerField)
    {
        if (int.TryParse(playerField, out int id))
            return lobbyManager?.Lobby.GetPlayerById(id);
        return lobbyManager?.Lobby.GetPlayer(playerField);
    }

    private void HandleButtonInput(ButtonInputEvent e)
    {
        if (!_running || _paused || _activeHit || _hitCooldown > 0f) return;
        if (e.state != ButtonState.Press) return;

        var player = ResolvePlayer(e.player);
        if (player == null) return;

        var symbolType = e.button == "button1" ? player.button1Symbol : player.button2Symbol;
        TryHitActive(player.id, symbolType);
    }

    private void HandleScratchInput(ScratchInputEvent e)
    {
        if (!_running || _paused || _activeHit || _hitCooldown > 0f) return;
        if (Mathf.Abs(e.velocity) < scratchVelocityThreshold) return;

        var player = ResolvePlayer(e.player);
        if (player == null) return;

        var symbolType = e.velocity > 0 ? SymbolType.ScratchPad_DOWN : SymbolType.ScratchPad_UP;
        TryHitActive(player.id, symbolType);
    }

    private void TryHitActive(int playerId, SymbolType expectedSymbol)
    {
        if (_currentIndex >= _sequence.Length) return;

        var active = _sequence[_currentIndex];

        // Wrong player — ignore (not their turn)
        if (active.playerId != playerId) return;

        // Right player, wrong input — penalize
        if (active.symbolType != expectedSymbol)
        {
            Debug.Log($"[SymbolScroller] WRONG input from player {playerId}: expected {active.symbolType}, got {expectedSymbol}");
            OnWrongInput?.Invoke();
            return;
        }

        _activeHit = true;

        // Calculate timing score based on how close to center of the beat
        int points = CalculateTimingPoints();
        string rating = GetTimingRating();
        OnTimingScored?.Invoke(points);

        // Freeze the indicator at its current position
        _indicatorFrozen = true;

        // Apply points to score data
        gameManager.ScoreData.score += points;

        // Send score to the player's phone
        if (lobbyManager != null)
            lobbyManager.SendScoreUpdate(playerId, gameManager.ScoreData.score, points, rating);

        Debug.Log($"[SymbolScroller] HIT! timing={_timer / beatInterval:P0} points={points} rating={rating}");

        OnSymbolResolved?.Invoke(active, true);
        AdvanceSymbol();
    }

    /// <summary>
    /// Returns points based on how close _timer is to the center of the beat.
    /// Center = beatInterval * 0.5 is "perfect". The further away, the fewer points.
    /// </summary>
    private int CalculateTimingPoints()
    {
        // Normalize timer to 0..1 range within the beat
        float progress = Mathf.Clamp01(_timer / beatInterval);
        // Distance from center (0 = perfect, 0.5 = worst)
        float distFromCenter = Mathf.Abs(progress - 0.5f);

        if (distFromCenter <= perfectWindow)
            return perfectPoints;
        if (distFromCenter <= goodWindow)
            return goodPoints;
        return okPoints;
    }

    private string GetTimingRating()
    {
        float progress = Mathf.Clamp01(_timer / beatInterval);
        float distFromCenter = Mathf.Abs(progress - 0.5f);

        if (distFromCenter <= perfectWindow) return "perfect";
        if (distFromCenter <= goodWindow)    return "good";
        return "ok";
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_running || _paused) return;

        // Tick down hit cooldown
        if (_hitCooldown > 0f)
        {
            _hitCooldown -= Time.deltaTime;
            if (_hitCooldown < 0f) _hitCooldown = 0f;
        }

        if (_currentIndex >= _sequence.Length) return;

        _timer += Time.deltaTime;

        // Rotate indicator around the active frame
        if (!_indicatorFrozen)
            UpdateIndicatorPosition();

        if (_timer >= beatInterval)
        {
            // Time ran out — miss
            if (!_activeHit)
            {
                OnSymbolResolved?.Invoke(_sequence[_currentIndex], false);
                OnTimingScored?.Invoke(0);
            }

            AdvanceSymbol();
        }
    }

    // ── Symbol management ─────────────────────────────────────────────────

    private void AdvanceSymbol()
    {
        _currentIndex++;
        _timer          = 0f;
        _activeHit      = false;
        _hitCooldown    = HIT_COOLDOWN_DURATION;
        _indicatorFrozen = false;

        if (_currentIndex >= _sequence.Length)
        {
            _running = false;
            ClearAll();
            Debug.Log("[SymbolScroller] Sequence complete.");
            OnSequenceComplete?.Invoke();
            return;
        }

        ShowActive();
        CreateIndicator();
        RefreshQueue();
    }

    private void ShowActive()
    {
        if (_activeGO != null)
            Destroy(_activeGO);

        var instance = _sequence[_currentIndex];

        _activeGO  = Instantiate(symbolPrefab, activeSlot);
        _activeImg = _activeGO.GetComponent<Image>();
        var rt     = _activeGO.GetComponent<RectTransform>();

        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        var sprite = gameManager.GetSprite(instance.symbolType);
        if (sprite != null)
            _activeImg.sprite = sprite;

        _activeImg.color = instance.color;
    }

    private void RefreshQueue()
    {
        // Destroy old queue tiles
        foreach (var (go, _) in _queueTiles)
            if (go != null) Destroy(go);
        _queueTiles.Clear();

        // Populate upcoming
        int start = _currentIndex + 1;
        int end   = Mathf.Min(start + visibleQueueSize, _sequence.Length);

        for (int i = start; i < end; i++)
        {
            var instance = _sequence[i];

            var go  = Instantiate(symbolPrefab, queueParent);
            var rt  = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            rt.sizeDelta = new Vector2(128f, 128f);

            var sprite = gameManager.GetSprite(instance.symbolType);
            if (sprite != null)
                img.sprite = sprite;

            img.color = instance.color;

            _queueTiles.Add((go, img));
        }
    }

    // ── Timing indicator ────────────────────────────────────────────────

    /// <summary>
    /// Creates a small circular dot that orbits the active slot to show timing.
    /// The dot starts at the top and rotates clockwise 360° over one beatInterval.
    /// </summary>
    private void CreateIndicator()
    {
        DestroyIndicator();

        _indicatorGO = new GameObject("TimingIndicator");
        _indicatorRT = _indicatorGO.AddComponent<RectTransform>();
        _indicatorImg = _indicatorGO.AddComponent<Image>();

        // Parent to activeSlot so it moves with it but render on top
        _indicatorRT.SetParent(activeSlot, false);

        // Make a circular dot
        _indicatorRT.sizeDelta = new Vector2(indicatorSize, indicatorSize);
        _indicatorImg.color = indicatorColor;

        // Start at top center
        _indicatorRT.anchorMin = new Vector2(0.5f, 0.5f);
        _indicatorRT.anchorMax = new Vector2(0.5f, 0.5f);

        _indicatorFrozen = false;
        UpdateIndicatorPosition();
    }

    /// <summary>
    /// Positions the indicator dot along a rectangular path around the active slot edge.
    /// Progress 0→1 maps to a full clockwise orbit starting from top-center.
    /// </summary>
    private void UpdateIndicatorPosition()
    {
        if (_indicatorRT == null || activeSlot == null) return;

        float progress = Mathf.Clamp01(_timer / beatInterval);

        // Orbit as a circle around the center of the active slot
        float halfW = activeSlot.rect.width  * 0.5f + indicatorOrbitPadding;
        float halfH = activeSlot.rect.height * 0.5f + indicatorOrbitPadding;

        // Start at top (π/2), move clockwise (subtract full rotation over progress)
        float angle = Mathf.PI * 0.5f - progress * Mathf.PI * 2f;
        float x = Mathf.Cos(angle) * halfW;
        float y = Mathf.Sin(angle) * halfH;

        _indicatorRT.anchoredPosition = new Vector2(x, y);

        // Color feedback: green near center of beat, yellow further, red at edges
        float distFromCenter = Mathf.Abs(progress - 0.5f);
        if (distFromCenter <= perfectWindow)
            _indicatorImg.color = Color.green;
        else if (distFromCenter <= goodWindow)
            _indicatorImg.color = Color.yellow;
        else
            _indicatorImg.color = indicatorColor;
    }

    private void DestroyIndicator()
    {
        if (_indicatorGO != null)
        {
            Destroy(_indicatorGO);
            _indicatorGO  = null;
            _indicatorRT  = null;
            _indicatorImg = null;
        }
    }

    private void ClearAll()
    {
        if (_activeGO != null)
        {
            Destroy(_activeGO);
            _activeGO  = null;
            _activeImg = null;
        }

        DestroyIndicator();

        foreach (var (go, _) in _queueTiles)
            if (go != null) Destroy(go);
        _queueTiles.Clear();
    }
}
