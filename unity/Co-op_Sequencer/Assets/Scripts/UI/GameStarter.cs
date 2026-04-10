using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Starts the round after showing an announcement (e.g. "Round 1!") on a
/// TextMeshPro element, then fades it out before the symbols begin scrolling.
/// Pauses the game when a player disconnects and resumes when all reconnect.
/// Attach anywhere in the game scene.
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager    gameManager;
    [SerializeField] private SymbolScroller symbolScroller;

    [Header("Announcement")]
    [SerializeField] private TMP_Text announcementText;
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float fadeDuration    = 0.5f;

    [Header("Round Settings")]
    [SerializeField] private int   totalRounds    = 3;
    [SerializeField] private float roundSpeed     = 1.5f;
    [SerializeField] private int   sequenceLength = 20;
    [SerializeField] private float delayBetweenRounds = 2f;
    [Tooltip("Each round the beat interval is multiplied by this (e.g. 0.85 = 15% faster).")]
    [SerializeField] private float speedMultiplierPerRound = 0.85f;

    private LobbyManager _lobbyManager;
    private bool _paused;
    private bool _roundStarted;
    private int  _currentRound = 0;
    private Coroutine _startCoroutine;

    void Start()
    {
        foreach (var lm in FindObjectsByType<LobbyManager>(FindObjectsSortMode.None))
        {
            if (lm.Lobby != null) { _lobbyManager = lm; break; }
        }

        if (_lobbyManager == null)
        {
            Debug.LogError("[GameStarter] No initialized LobbyManager found.");
            return;
        }

        _lobbyManager.OnPlayerDisconnected += OnPlayerDisconnected;
        _lobbyManager.OnPlayerReconnected  += OnPlayerReconnected;
        symbolScroller.OnSequenceComplete  += OnRoundComplete;

        _startCoroutine = StartCoroutine(StartRoundSequence());
    }

    void OnDestroy()
    {
        if (_lobbyManager != null)
        {
            _lobbyManager.OnPlayerDisconnected -= OnPlayerDisconnected;
            _lobbyManager.OnPlayerReconnected  -= OnPlayerReconnected;
        }
        if (symbolScroller != null)
            symbolScroller.OnSequenceComplete -= OnRoundComplete;
    }

    // ── Pause / Resume ───────────────────────────────────────────────────

    private void OnPlayerDisconnected(Player player)
    {
        if (_paused) return;
        _paused = true;

        symbolScroller.SetPaused(true);

        if (announcementText != null)
        {
            // Only cancel announcement fades, not the start coroutine
            announcementText.text  = "Waiting for players...";
            announcementText.alpha = 1f;
            announcementText.gameObject.SetActive(true);
        }

        Debug.Log("[GameStarter] Game paused — player disconnected.");
    }

    private void OnPlayerReconnected(Player player)
    {
        if (!_paused) return;
        if (!_lobbyManager.AllPlayersConnected) return;

        _paused = false;

        if (_roundStarted)
        {
            // Round was already running — just resume
            symbolScroller.SetPaused(false);
            if (announcementText != null)
                StartCoroutine(ShowAnnouncementCoroutine("Go!"));
        }
        else
        {
            // Round never started (disconnect happened during the countdown) — start fresh
            if (announcementText != null)
                announcementText.gameObject.SetActive(false);
            _startCoroutine = StartCoroutine(StartRoundSequence());
        }

        Debug.Log("[GameStarter] Game resumed — all players reconnected.");
    }

    // ── Round lifecycle ────────────────────────────────────────────────

    private void OnRoundComplete()
    {
        _roundStarted = false;

        if (_currentRound >= totalRounds)
        {
            Debug.Log("[GameStarter] All rounds complete!");
            StartCoroutine(ShowAnnouncementCoroutine("Game Over!"));
            return;
        }

        StartCoroutine(NextRoundSequence());
    }

    private IEnumerator NextRoundSequence()
    {
        yield return StartCoroutine(ShowAnnouncementCoroutine($"Round {_currentRound} Complete!"));
        yield return new WaitForSeconds(delayBetweenRounds);
        _startCoroutine = StartCoroutine(StartRoundSequence());
    }

    private IEnumerator StartRoundSequence()
    {
        _currentRound++;

        gameManager.StartNewRound(roundSpeed, sequenceLength, _lobbyManager.Lobby.players);
        _lobbyManager.SendSymbolAssignments();

        if (announcementText != null)
        {
            announcementText.text  = $"Round {_currentRound}!";
            announcementText.alpha = 1f;
            announcementText.gameObject.SetActive(true);

            yield return new WaitForSeconds(displayDuration);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                announcementText.alpha = 1f - (elapsed / fadeDuration);
                yield return null;
            }

            announcementText.alpha = 0f;
            announcementText.gameObject.SetActive(false);
        }

        // Speed up the beat interval each round
        if (_currentRound > 1)
            symbolScroller.BeatInterval *= speedMultiplierPerRound;

        _roundStarted = true;
        symbolScroller.StartScrolling();
    }

    // ── Announcement helper ──────────────────────────────────────────────

    public void ShowAnnouncement(string message)
    {
        if (announcementText != null)
            StartCoroutine(ShowAnnouncementCoroutine(message));
    }

    private IEnumerator ShowAnnouncementCoroutine(string message)
    {
        announcementText.text  = message;
        announcementText.alpha = 1f;
        announcementText.gameObject.SetActive(true);

        yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            announcementText.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        announcementText.alpha = 0f;
        announcementText.gameObject.SetActive(false);
    }
}
