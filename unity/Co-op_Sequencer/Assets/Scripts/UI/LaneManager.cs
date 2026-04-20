using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dynamically creates and manages lanes based on the number of players.
/// Total lanes = player count + 1 (always one empty lane).
/// Routes scratch/button input to the lane the player is currently on.
/// </summary>
public class LaneManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject lanePrefab;
    [SerializeField] private GameObject symbolPrefab;
    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private LobbyManager lobbyManagerRef;

    [Header("Layout")]
    [SerializeField] private RectTransform laneContainer;
    [SerializeField] private float minLaneHeight = 80f;
    [SerializeField] private float maxLaneHeight = 130f;
    [SerializeField] private float minPlayerIconsWidth = 40f;
    [SerializeField] private float maxPlayerIconsWidth = 80f;

    [Header("Scroll Settings")]
    [Tooltip("How many beats it takes a symbol to travel from spawn to the timing zone.")]
    [SerializeField] private int beatsToReachTiming = 4;
    [Tooltip("Chance (0-1) that symbols spawn on any given beat. Lower = more breather moments.")]
    [Range(0f, 1f)]
    [SerializeField] private float symbolSpawnChance = 0.75f;

    public float SymbolSpawnChance => symbolSpawnChance;

    [Header("Scoring")]
    [SerializeField] private int   perfectPoints = 100;
    [SerializeField] private int   goodPoints    = 50;
    [SerializeField] private int   okPoints      = 25;
    [Tooltip("Normalized distance from center (0-1) for perfect score.")]
    [SerializeField] private float perfectWindow = 0.15f;
    [Tooltip("Normalized distance from center (0-1) for good score.")]
    [SerializeField] private float goodWindow    = 0.40f;
    [Tooltip("Fallback hit window in beats if no TimingZone rect is found.")]
    [SerializeField] private float fallbackHitWindowBeats = 0.5f;

    /// <summary>Fired when a player switches lane. (playerId, newLane)</summary>
    public event Action<int, int> OnPlayerLaneChanged;

    /// <summary>Fired when ALL lane scrollers have completed their sequence.</summary>
    public event Action OnAllLanesComplete;

    public int TotalLanes { get; private set; }

    private readonly List<Lane> _lanes = new();
    private int _lanesCompleted;
    private LobbyManager _lobbyManagerCached;

    /// <summary>Lazily resolves the LobbyManager, surviving scene transitions.</summary>
    private LobbyManager lobbyManager
    {
        get
        {
            if (_lobbyManagerCached != null) return _lobbyManagerCached;
            if (lobbyManagerRef != null && lobbyManagerRef.Lobby != null)
            {
                _lobbyManagerCached = lobbyManagerRef;
                return _lobbyManagerCached;
            }
            foreach (var lm in FindObjectsByType<LobbyManager>(FindObjectsSortMode.None))
            {
                if (lm.Lobby != null) { _lobbyManagerCached = lm; return _lobbyManagerCached; }
            }
            return null;
        }
    }

    void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (audioManager == null)
            audioManager = FindFirstObjectByType<AudioManager>();
    }

    void OnEnable()
    {
        InputReceiver.OnSliderInput  += HandleSliderInput;
        InputReceiver.OnScratchInput += HandleScratchInput;
        InputReceiver.OnButtonInput  += HandleButtonInput;
    }

    void OnDisable()
    {
        InputReceiver.OnSliderInput  -= HandleSliderInput;
        InputReceiver.OnScratchInput -= HandleScratchInput;
        InputReceiver.OnButtonInput  -= HandleButtonInput;
    }

    // ── Setup ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates lanes and initializes their scrollers.
    /// Call when the game starts.
    /// </summary>
    public void SetupLanes(int playerCount)
    {
        ClearLanes();

        TotalLanes = playerCount + 1;

        // Calculate lane height: divide available space, clamped between min and max
        float containerHeight = laneContainer.rect.height;
        float laneHeight = Mathf.Clamp(containerHeight / TotalLanes, minLaneHeight, maxLaneHeight);

        for (int i = 0; i < TotalLanes; i++)
        {
            var laneGO = Instantiate(lanePrefab, laneContainer);
            laneGO.name = $"Lane_{i}";

            // Set lane height via LayoutElement
            var layoutElement = laneGO.GetComponent<UnityEngine.UI.LayoutElement>();
            if (layoutElement == null) layoutElement = laneGO.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutElement.preferredHeight = laneHeight;
            layoutElement.flexibleHeight = 0;

            var lane = laneGO.GetComponent<Lane>();
            if (lane == null) lane = laneGO.AddComponent<Lane>();

            lane.LaneIndex = i;
            lane.SetPlayerIconsWidthRange(minPlayerIconsWidth, maxPlayerIconsWidth);
            lane.SetPlayerIconPrefab(playerIconPrefab);
            lane.SetupScroller(symbolPrefab, gameManager, audioManager);
            lane.Scroller.ApplySettings(beatsToReachTiming, fallbackHitWindowBeats,
                                        perfectPoints, goodPoints, okPoints,
                                        perfectWindow, goodWindow);
            _lanes.Add(lane);
        }

        // Assign players to starting lanes
        if (lobbyManager?.Lobby != null)
        {
            foreach (var player in lobbyManager.Lobby.players)
            {
                player.lane = Mathf.Min(player.id, TotalLanes - 1);
                lobbyManager.SendLaneUpdate(player.id, player.lane, TotalLanes);
            }
        }

        RefreshAllPlayerIcons();
        Debug.Log($"[LaneManager] Created {TotalLanes} lanes for {playerCount} players");
    }

    // ── Round control ────────────────────────────────────────────────────

    /// <summary>
    /// Starts scrolling on all lanes with their assigned sequences.
    /// Call after setting sequences via SetLaneSequence.
    /// </summary>
    public void StartAllScrollers()
    {
        _lanesCompleted = 0;

        foreach (var lane in _lanes)
        {
            lane.Scroller.OnSequenceComplete -= OnLaneComplete;
            lane.Scroller.OnSequenceComplete += OnLaneComplete;
        }
    }

    /// <summary>Starts all lanes with their sequences, synced to the same start beat.</summary>
    public void StartAllLaneSequences(SymbolInstance[][] sequences)
    {
        _lanesCompleted = 0;

        // Calculate a shared start beat so all lanes are in sync
        int startBeat = beatsToReachTiming;
        if (audioManager != null)
            startBeat = audioManager.CurrentBeat + 1 + beatsToReachTiming;

        for (int i = 0; i < _lanes.Count && i < sequences.Length; i++)
        {
            var lane = _lanes[i];
            lane.Scroller.OnSequenceComplete -= OnLaneComplete;
            lane.Scroller.OnSequenceComplete += OnLaneComplete;
            lane.Scroller.StartScrolling(sequences[i], startBeat);
        }
    }

    /// <summary>Sets the symbol sequence for a specific lane and starts it.</summary>
    public void StartLaneSequence(int laneIndex, SymbolInstance[] sequence)
    {
        var lane = GetLane(laneIndex);
        if (lane == null) return;

        lane.Scroller.OnSequenceComplete -= OnLaneComplete;
        lane.Scroller.OnSequenceComplete += OnLaneComplete;
        lane.Scroller.StartScrolling(sequence);
    }

    public void SetAllPaused(bool paused)
    {
        foreach (var lane in _lanes)
            lane.Scroller.SetPaused(paused);
    }

    public void StopAllScrollers()
    {
        foreach (var lane in _lanes)
        {
            lane.Scroller.OnSequenceComplete -= OnLaneComplete;
            lane.Scroller.StopScrolling();
        }
    }

    private void OnLaneComplete()
    {
        _lanesCompleted++;
        if (_lanesCompleted >= TotalLanes)
        {
            Debug.Log("[LaneManager] All lanes completed.");
            OnAllLanesComplete?.Invoke();
        }
    }

    // ── Lane access ──────────────────────────────────────────────────────

    public Lane GetLane(int index)
    {
        if (index < 0 || index >= _lanes.Count) return null;
        return _lanes[index];
    }

    public List<Player> GetPlayersOnLane(int laneIndex)
    {
        var result = new List<Player>();
        if (lobbyManager?.Lobby == null) return result;

        foreach (var player in lobbyManager.Lobby.players)
        {
            if (player.connected && player.lane == laneIndex)
                result.Add(player);
        }
        return result;
    }

    // ── Player movement ──────────────────────────────────────────────────

    public void MovePlayer(int playerId, int delta)
    {
        var player = lobbyManager?.Lobby?.GetPlayerById(playerId);
        if (player == null) return;

        int newLane = Mathf.Clamp(player.lane + delta, 0, TotalLanes - 1);
        if (newLane == player.lane) return;

        player.lane = newLane;
        lobbyManager.SendLaneUpdate(playerId, newLane, TotalLanes);
        RefreshAllPlayerIcons();

        Debug.Log($"[LaneManager] Player {playerId} moved to lane {newLane}");
        OnPlayerLaneChanged?.Invoke(playerId, newLane);
    }

    private void RefreshAllPlayerIcons()
    {
        if (lobbyManager?.Lobby == null)
        {
            Debug.LogWarning("[LaneManager] RefreshIcons: lobbyManager or Lobby is null!");
            return;
        }

        foreach (var p in lobbyManager.Lobby.players)
            Debug.Log($"[LaneManager] Player {p.id}: lane={p.lane}, connected={p.connected}");

        for (int i = 0; i < _lanes.Count; i++)
        {
            var players = GetPlayersOnLane(i);
            Debug.Log($"[LaneManager] RefreshIcons: lane {i} has {players.Count} player(s)");
            _lanes[i].SetPlayers(players, gameManager);
        }
    }

    // ── Input routing ────────────────────────────────────────────────────

    private Player ResolvePlayer(string playerField)
    {
        if (int.TryParse(playerField, out int id))
            return lobbyManager?.Lobby?.GetPlayerById(id);
        return lobbyManager?.Lobby?.GetPlayer(playerField);
    }

    private void HandleSliderInput(SliderInputEvent e)
    {
        var player = ResolvePlayer(e.player);
        if (player == null) return;

        int delta = e.direction == SliderDirection.Up ? -1 : 1;
        MovePlayer(player.id, delta);
    }

    private void HandleScratchInput(ScratchInputEvent e)
    {
        var player = ResolvePlayer(e.player);
        if (player == null) return;

        // Route to the scroller of the lane the player is on
        var lane = GetLane(player.lane);
        lane?.Scroller.HandleScratchInput(player.id, e.velocity);
    }

    private void HandleButtonInput(ButtonInputEvent e)
    {
        // Buttons removed from controller — kept as legacy stub
    }

    private void ClearLanes()
    {
        foreach (var lane in _lanes)
        {
            if (lane != null)
            {
                lane.Scroller.OnSequenceComplete -= OnLaneComplete;
                Destroy(lane.gameObject);
            }
        }
        _lanes.Clear();
        TotalLanes = 0;
    }
}
