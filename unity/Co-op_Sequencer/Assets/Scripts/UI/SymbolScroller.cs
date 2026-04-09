using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a queue of upcoming symbols with the active one in a highlight box.
/// Players must perform the correct action within a time window.
/// On success or timeout, the active symbol is removed and the queue shifts up.
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

    [Header("Scratch")]
    [SerializeField] private float scratchVelocityThreshold = 3f;

    /// <summary>Fired when the active symbol is resolved (hit or missed). Bool = was it a hit.</summary>
    public event Action<SymbolInstance, bool> OnSymbolResolved;

    // State
    private SymbolInstance[] _sequence;
    private int   _currentIndex = 0;
    private float _timer        = 0f;
    private bool  _running      = false;
    private bool  _activeHit    = false;

    // Active symbol UI
    private GameObject _activeGO;
    private Image      _activeImg;

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

        ClearAll();
        ShowActive();
        RefreshQueue();
    }

    public void StopScrolling()
    {
        _running = false;
        ClearAll();
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

    private void HandleButtonInput(ButtonInputEvent e)
    {
        if (!_running || _activeHit) return;
        if (e.state != ButtonState.Press) return;

        var player = lobbyManager?.Lobby.GetPlayer(e.player);
        if (player == null) return;

        var symbolType = e.button == "button1" ? player.button1Symbol : player.button2Symbol;
        TryHitActive(player.id, symbolType);
    }

    private void HandleScratchInput(ScratchInputEvent e)
    {
        if (!_running || _activeHit) return;
        if (Mathf.Abs(e.velocity) < scratchVelocityThreshold) return;

        var player = lobbyManager?.Lobby.GetPlayer(e.player);
        if (player == null) return;

        var symbolType = e.velocity > 0 ? SymbolType.ScratchPad_DOWN : SymbolType.ScratchPad_UP;
        TryHitActive(player.id, symbolType);
    }

    private void TryHitActive(int playerId, SymbolType expectedSymbol)
    {
        if (_currentIndex >= _sequence.Length) return;

        var active = _sequence[_currentIndex];
        if (active.playerId != playerId) return;
        if (active.symbolType != expectedSymbol) return;

        _activeHit = true;
        OnSymbolResolved?.Invoke(active, true);
        AdvanceSymbol();
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_running) return;
        if (_currentIndex >= _sequence.Length) return;

        _timer += Time.deltaTime;

        if (_timer >= beatInterval)
        {
            // Time ran out — miss
            if (!_activeHit)
                OnSymbolResolved?.Invoke(_sequence[_currentIndex], false);

            AdvanceSymbol();
        }
    }

    // ── Symbol management ─────────────────────────────────────────────────

    private void AdvanceSymbol()
    {
        _currentIndex++;
        _timer     = 0f;
        _activeHit = false;

        if (_currentIndex >= _sequence.Length)
        {
            _running = false;
            ClearAll();
            Debug.Log("[SymbolScroller] Sequence complete.");
            return;
        }

        ShowActive();
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

    private void ClearAll()
    {
        if (_activeGO != null)
        {
            Destroy(_activeGO);
            _activeGO  = null;
            _activeImg = null;
        }

        foreach (var (go, _) in _queueTiles)
            if (go != null) Destroy(go);
        _queueTiles.Clear();
    }
}
