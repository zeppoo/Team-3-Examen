using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-lane symbol scroller. Symbols slide smoothly from right to left,
/// synced to the AudioManager's DSP clock so they always land on-beat.
///
/// Hit detection: any symbol currently overlapping the timing zone can be hit.
/// The closest symbol to the timing zone center is checked first.
/// </summary>
public class SymbolScroller : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float symbolSize = 80f;
    [SerializeField] private float scratchVelocityThreshold = 3f;
    [SerializeField] private int beatsToReachTiming = 4;

    [Header("Timing Zone")]
    [Tooltip("Fallback hit window in beats if no TimingZone rect is assigned.")]
    [SerializeField] private float fallbackHitWindowBeats = 0.5f;

    [Header("Scoring")]
    [SerializeField] private int   perfectPoints = 100;
    [SerializeField] private int   goodPoints    = 50;
    [SerializeField] private int   okPoints      = 25;
    [SerializeField] private float perfectWindow = 0.15f;
    [SerializeField] private float goodWindow    = 0.40f;

    [Header("Hit Feedback")]
    [SerializeField] private float correctFadeDuration   = 0.3f;
    [SerializeField] private float correctScaleMultiplier = 1.4f;
    [SerializeField] private float wrongFadeDuration      = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool showTimingDebug = true;

    public int LaneIndex { get; set; }

    // Events
    public event Action<SymbolInstance, bool> OnSymbolResolved;
    public event Action<int> OnTimingScored;
    public event Action OnWrongInput;
    public event Action OnSequenceComplete;

    // References (set via Initialize)
    private RectTransform _scrollArea;
    private RectTransform _symbolContainer;
    private RectTransform _timingZoneRT;
    private GameObject _symbolPrefab;
    private GameManager _gameManager;
    private AudioManager _audioManager;

    // Layout
    private float _timingX;
    private float _timingHalfWidth;
    private float _hitWindowBeats;
    private float _spawnX;
    private float _destroyX;
    private float _pixelsPerBeat;
    private bool _layoutDirty;

    // State
    private SymbolInstance[] _sequence;
    private int   _nextSpawnIndex;
    private bool  _running;
    private bool  _paused;
    private int   _resolvedCount; // how many symbols have been resolved (hit or missed)

    // Beat sync
    private int _startBeat;

    private float SecPerBeat => _audioManager != null ? _audioManager.SecondsPerBeat : 0.5f;

    // Live symbol tiles
    private readonly List<SymbolTile> _tiles = new();

    private class SymbolTile
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public int sequenceIndex;
        public bool resolved; // already hit or missed — skip during detection
    }

    // ── Configuration ────────────────────────────────────────────────────

    public void Initialize(RectTransform scrollArea, RectTransform symbolContainer,
                           RectTransform timingZone, GameObject symbolPrefab,
                           GameManager gameManager, AudioManager audioManager)
    {
        _scrollArea      = scrollArea;
        _symbolContainer = symbolContainer;
        _timingZoneRT    = timingZone;
        _symbolPrefab    = symbolPrefab;
        _gameManager     = gameManager;
        _audioManager    = audioManager;
    }

    /// <summary>Apply scroll and scoring settings from LaneManager.</summary>
    public void ApplySettings(int beatsToReach, float fallbackHitWindow,
                              int perfect, int good, int ok,
                              float perfectWin, float goodWin)
    {
        beatsToReachTiming    = beatsToReach;
        fallbackHitWindowBeats = fallbackHitWindow;
        perfectPoints         = perfect;
        goodPoints            = good;
        okPoints              = ok;
        perfectWindow         = perfectWin;
        goodWindow            = goodWin;
    }

    // ── Public API ──────────────────────────────────────────────────────

    public void StartScrolling(SymbolInstance[] sequence, int startBeat = -1)
    {
        _sequence       = sequence;
        _nextSpawnIndex = 0;
        _resolvedCount  = 0;
        _running        = true;
        _paused         = false;

        ClearAllTiles();
        CalculateLayout();

        if (startBeat >= 0)
        {
            _startBeat = startBeat;
        }
        else if (_audioManager != null)
        {
            _startBeat = _audioManager.CurrentBeat + 1 + beatsToReachTiming;
        }
        else
        {
            _startBeat = beatsToReachTiming;
        }

        // Count non-null symbols and pre-resolve nulls
        int actualSymbols = 0;
        for (int i = 0; i < _sequence.Length; i++)
        {
            if (_sequence[i] != null)
                actualSymbols++;
            else
                _resolvedCount++; // null = no symbol on this beat, auto-resolve
        }

        Debug.Log($"[SymbolScroller] Lane {LaneIndex}: {actualSymbols} symbols across {_sequence.Length} beats, " +
                  $"startBeat={_startBeat}, hitWindow={_hitWindowBeats:F2} beats");
    }

    public void StopScrolling()
    {
        _running = false;
        ClearAllTiles();
    }

    public void SetPaused(bool paused) => _paused = paused;

    // ── Input (called by LaneManager) ───────────────────────────────────

    public void HandleScratchInput(int playerId, float velocity)
    {
        if (!_running || _paused) return;
        if (Mathf.Abs(velocity) < scratchVelocityThreshold) return;

        var symbolType = velocity > 0 ? SymbolType.ScratchPad_DOWN : SymbolType.ScratchPad_UP;
        TryHit(playerId, symbolType);
    }

    public void HandleButtonInput(int playerId, SymbolType symbolType)
    {
        if (!_running || _paused) return;
        TryHit(playerId, symbolType);
    }

    // ── Hit logic ───────────────────────────────────────────────────────

    private void TryHit(int playerId, SymbolType inputSymbol)
    {
        float currentBeatF = GetCurrentBeatFractional();

        // Find the closest unresolved symbol that is inside the timing zone
        SymbolTile bestTile = null;
        float bestDist = float.MaxValue;

        foreach (var tile in _tiles)
        {
            if (tile.resolved) continue;

            float targetBeat = _startBeat + tile.sequenceIndex;
            float beatsFromCenter = Mathf.Abs(currentBeatF - targetBeat);

            if (beatsFromCenter <= _hitWindowBeats && beatsFromCenter < bestDist)
            {
                bestDist = beatsFromCenter;
                bestTile = tile;
            }
        }

        if (bestTile == null)
        {
            Debug.Log($"[SymbolScroller] Lane {LaneIndex}: input from player {playerId} but no symbol in timing zone (beat={currentBeatF:F2})");
            return;
        }

        var active = _sequence[bestTile.sequenceIndex];
        bestTile.resolved = true;
        _resolvedCount++;

        if (active.symbolType != inputSymbol)
        {
            Debug.Log($"[SymbolScroller] Lane {LaneIndex}: WRONG from player {playerId}: expected {active.symbolType}, got {inputSymbol}");
            OnWrongInput?.Invoke();
            OnSymbolResolved?.Invoke(active, false);
            OnTimingScored?.Invoke(0);
            StartCoroutine(WrongHitAnimation(bestTile));
        }
        else
        {
            int points = CalculateTimingPoints(bestDist);
            OnTimingScored?.Invoke(points);

            if (_gameManager != null)
                _gameManager.ScoreData.score += points;

            Debug.Log($"[SymbolScroller] Lane {LaneIndex}: HIT by player {playerId}! dist={bestDist:F3} beats, points={points}");
            OnSymbolResolved?.Invoke(active, true);
            StartCoroutine(CorrectHitAnimation(bestTile));
        }

        CheckSequenceComplete();
    }

    private int CalculateTimingPoints(float beatsFromCenter)
    {
        float normalized = _hitWindowBeats > 0 ? beatsFromCenter / _hitWindowBeats : 1f;

        if (normalized <= perfectWindow) return perfectPoints;
        if (normalized <= goodWindow)    return goodPoints;
        return okPoints;
    }

    // ── Hit animations ──────────────────────────────────────────────────

    private IEnumerator CorrectHitAnimation(SymbolTile tile)
    {
        Vector3 startScale = tile.rt.localScale;
        Vector3 targetScale = startScale * correctScaleMultiplier;
        Color startColor = tile.img != null ? tile.img.color : Color.white;

        float elapsed = 0f;
        while (elapsed < correctFadeDuration)
        {
            if (tile.go == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / correctFadeDuration;

            tile.rt.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (tile.img != null)
            {
                var c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                tile.img.color = c;
            }

            yield return null;
        }

        DestroyTile(tile);
    }

    private IEnumerator WrongHitAnimation(SymbolTile tile)
    {
        if (tile.img != null)
            tile.img.color = Color.red;

        float elapsed = 0f;
        while (elapsed < wrongFadeDuration)
        {
            if (tile.go == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / wrongFadeDuration;

            if (tile.img != null)
            {
                var c = Color.red;
                c.a = Mathf.Lerp(1f, 0f, t);
                tile.img.color = c;
            }

            yield return null;
        }

        DestroyTile(tile);
    }

    private void DestroyTile(SymbolTile tile)
    {
        if (tile.go != null)
            Destroy(tile.go);
        _tiles.Remove(tile);
    }

    // ── Update ──────────────────────────────────────────────────────────

    void Update()
    {
        if (!_running || _paused) return;

        if (_layoutDirty)
        {
            CalculateLayout();
            if (_layoutDirty) return;
        }

        float currentBeatF = GetCurrentBeatFractional();

        SpawnUpcomingTiles(currentBeatF);

        // Position tiles and detect misses
        for (int i = _tiles.Count - 1; i >= 0; i--)
        {
            var tile = _tiles[i];
            float targetBeat = _startBeat + tile.sequenceIndex;
            float beatsUntilArrival = targetBeat - currentBeatF;
            float xPos = _timingX + beatsUntilArrival * _pixelsPerBeat;

            // Resolved tiles (animating) — still move but don't check for miss
            tile.rt.anchoredPosition = new Vector2(xPos, 0f);

            if (xPos < _destroyX)
            {
                if (!tile.resolved)
                {
                    tile.resolved = true;
                    _resolvedCount++;
                    var sym = _sequence[tile.sequenceIndex];
                    if (sym != null)
                    {
                        OnSymbolResolved?.Invoke(sym, false);
                        OnTimingScored?.Invoke(0);
                    }
                }
                Destroy(tile.go);
                _tiles.RemoveAt(i);
                CheckSequenceComplete();
                continue;
            }

            if (!tile.resolved)
            {
                float beatsPast = currentBeatF - targetBeat;
                if (beatsPast > _hitWindowBeats)
                {
                    tile.resolved = true;
                    _resolvedCount++;
                    var sym = _sequence[tile.sequenceIndex];
                    if (sym != null)
                    {
                        OnSymbolResolved?.Invoke(sym, false);
                        OnTimingScored?.Invoke(0);
                    }
                    CheckSequenceComplete();
                }
            }
        }
    }

    private float GetCurrentBeatFractional()
    {
        if (_audioManager == null) return 0f;

        double elapsed = AudioSettings.dspTime - _audioManager.MusicStartDspTime;
        if (elapsed < 0) return 0f;
        return (float)(elapsed / SecPerBeat);
    }

    private void CheckSequenceComplete()
    {
        if (_sequence != null && _resolvedCount >= _sequence.Length)
        {
            _running = false;
            Debug.Log($"[SymbolScroller] Lane {LaneIndex}: sequence complete.");
            OnSequenceComplete?.Invoke();
        }
    }

    // ── Layout calculation ──────────────────────────────────────────────

    private void CalculateLayout()
    {
        if (_scrollArea == null) return;

        float areaWidth = _scrollArea.rect.width;

        if (areaWidth <= 0f)
        {
            _layoutDirty = true;
            return;
        }
        _layoutDirty = false;

        if (_timingZoneRT != null)
        {
            // Convert timing zone center to scroll area local space (center-origin),
            // matching the coordinate space symbols use (anchors at 0.5, 0.5).
            Vector3 timingWorldPos = _timingZoneRT.position;
            Vector3 localPos = _scrollArea.InverseTransformPoint(timingWorldPos);
            _timingX = localPos.x;
            _timingHalfWidth = _timingZoneRT.rect.width * 0.5f;
        }
        else
        {
            _timingX = -areaWidth * 0.5f + symbolSize;
            _timingHalfWidth = 0f;
        }

        _spawnX = areaWidth * 0.5f;
        _destroyX = -areaWidth * 0.5f - symbolSize;

        float travelDist = _spawnX - _timingX;
        _pixelsPerBeat = travelDist / beatsToReachTiming;

        if (_timingHalfWidth > 0f && _pixelsPerBeat > 0f)
            _hitWindowBeats = _timingHalfWidth / _pixelsPerBeat;
        else
            _hitWindowBeats = fallbackHitWindowBeats;

        Debug.Log($"[SymbolScroller] Lane {LaneIndex}: timingX={_timingX:F0}, zoneWidth={_timingHalfWidth * 2:F0}px, " +
                  $"hitWindow={_hitWindowBeats:F2} beats, {_pixelsPerBeat:F0}px/beat");

        if (_scrollArea.GetComponent<RectMask2D>() == null)
            _scrollArea.gameObject.AddComponent<RectMask2D>();
    }

    // ── Tile spawning ───────────────────────────────────────────────────

    private void SpawnUpcomingTiles(float currentBeatF)
    {
        if (_symbolContainer == null || _symbolPrefab == null) return;

        while (_nextSpawnIndex < _sequence.Length)
        {
            // Skip null entries (empty beats for this lane)
            if (_sequence[_nextSpawnIndex] == null)
            {
                _nextSpawnIndex++;
                continue;
            }

            float targetBeat = _startBeat + _nextSpawnIndex;
            float beatsUntilArrival = targetBeat - currentBeatF;
            float xPos = _timingX + beatsUntilArrival * _pixelsPerBeat;

            if (xPos > _spawnX + symbolSize)
                break;

            SpawnTile(_nextSpawnIndex, xPos);
            _nextSpawnIndex++;
        }
    }

    private void SpawnTile(int index, float xPos)
    {
        var instance = _sequence[index];

        var go = Instantiate(_symbolPrefab, _symbolContainer);
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(symbolSize, symbolSize);
        rt.anchoredPosition = new Vector2(xPos, 0f);

        var sprite = _gameManager?.GetSprite(instance.symbolType);
        if (sprite != null && img != null) img.sprite = sprite;
        if (img != null) img.color = instance.color;

        _tiles.Add(new SymbolTile
        {
            go = go,
            rt = rt,
            img = img,
            sequenceIndex = index,
            resolved = false
        });
    }

    // ── Cleanup ─────────────────────────────────────────────────────────

    private void ClearAllTiles()
    {
        StopAllCoroutines();
        foreach (var tile in _tiles)
            if (tile.go != null) Destroy(tile.go);
        _tiles.Clear();
    }

    // ── Debug visualization ─────────────────────────────────────────────

    void OnGUI()
    {
        if (!showTimingDebug || !_running || _scrollArea == null) return;

        // Get the canvas so we can convert local positions to screen positions
        var canvas = _scrollArea.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // Timing zone center line (green)
        Vector2 centerLocal = new Vector2(_timingX, 0f);
        Vector3 centerWorld = _scrollArea.TransformPoint(centerLocal);
        Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(cam, centerWorld);
        // Flip Y for OnGUI (screen space is bottom-up, GUI is top-down)
        float centerScreenY = Screen.height - centerScreen.y;

        // Left edge of timing zone (yellow)
        float leftX = _timingX - _hitWindowBeats * _pixelsPerBeat;
        Vector3 leftWorld = _scrollArea.TransformPoint(new Vector2(leftX, 0f));
        Vector2 leftScreen = RectTransformUtility.WorldToScreenPoint(cam, leftWorld);
        float leftScreenX = leftScreen.x;

        // Right edge of timing zone (yellow)
        float rightX = _timingX + _hitWindowBeats * _pixelsPerBeat;
        Vector3 rightWorld = _scrollArea.TransformPoint(new Vector2(rightX, 0f));
        Vector2 rightScreen = RectTransformUtility.WorldToScreenPoint(cam, rightWorld);
        float rightScreenX = rightScreen.x;

        // Get lane vertical bounds
        float laneTop = centerScreen.y - _scrollArea.rect.height * 0.5f;
        float laneBottom = centerScreen.y + _scrollArea.rect.height * 0.5f;
        float laneTopGUI = Screen.height - laneBottom;
        float laneBottomGUI = Screen.height - laneTop;
        float laneHeight = laneBottomGUI - laneTopGUI;

        // Draw hit window zone (semi-transparent yellow)
        var zoneRect = new Rect(leftScreenX, laneTopGUI, rightScreenX - leftScreenX, laneHeight);
        var prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 0f, 0.15f);
        GUI.DrawTexture(zoneRect, Texture2D.whiteTexture);

        // Draw left/right edges (yellow lines)
        GUI.color = new Color(1f, 1f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(leftScreenX - 1, laneTopGUI, 2, laneHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rightScreenX - 1, laneTopGUI, 2, laneHeight), Texture2D.whiteTexture);

        // Draw center line (green)
        GUI.color = new Color(0f, 1f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(centerScreen.x - 1, laneTopGUI, 2, laneHeight), Texture2D.whiteTexture);

        // Draw perfect zone (cyan, inner area)
        float perfectPixels = perfectWindow * _hitWindowBeats * _pixelsPerBeat;
        float perfectLeftX = _timingX - perfectPixels;
        float perfectRightX = _timingX + perfectPixels;
        Vector3 pLeftWorld = _scrollArea.TransformPoint(new Vector2(perfectLeftX, 0f));
        Vector3 pRightWorld = _scrollArea.TransformPoint(new Vector2(perfectRightX, 0f));
        Vector2 pLeftScreen = RectTransformUtility.WorldToScreenPoint(cam, pLeftWorld);
        Vector2 pRightScreen = RectTransformUtility.WorldToScreenPoint(cam, pRightWorld);
        GUI.color = new Color(0f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(pLeftScreen.x, laneTopGUI, pRightScreen.x - pLeftScreen.x, laneHeight), Texture2D.whiteTexture);

        // Label
        GUI.color = Color.white;
        GUI.Label(new Rect(leftScreenX, laneTopGUI - 18, 200, 20),
            $"L{LaneIndex}: {_hitWindowBeats:F2}b window");

        GUI.color = prevColor;
    }
}
