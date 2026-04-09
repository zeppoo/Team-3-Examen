using UnityEngine;

/// <summary>
/// Press S to start the round, assign symbols and begin scrolling.
/// Attach anywhere in the game scene.
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager    gameManager;
    [SerializeField] private SymbolScroller symbolScroller;

    [Header("Round Settings")]
    [SerializeField] private float roundSpeed     = 1.5f;
    [SerializeField] private int   sequenceLength = 20;

    private LobbyManager _lobbyManager;

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
        StartRound();
    }

    private void StartRound()
    {
        gameManager.StartNewRound(roundSpeed, sequenceLength, _lobbyManager.Lobby.players);
        _lobbyManager.SendSymbolAssignments();
        symbolScroller.StartScrolling();
    }
}
