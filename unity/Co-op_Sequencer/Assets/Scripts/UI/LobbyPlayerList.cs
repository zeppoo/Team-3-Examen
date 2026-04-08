using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays the current player count and a list of connected players.
///
/// Required scene setup:
///   - GameObject with this component
///   - LobbyManager somewhere in the scene
///   - TMP_Text → assign to playerCountText  (shows e.g. "(2/4)")
///   - TMP_Text → assign to playerListText   (shows one player per line)
/// </summary>
public class LobbyPlayerList : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Text showing current/max players, e.g. \"(2/4)\".")]
    public TMP_Text playerCountText;

    [Tooltip("Text listing all connected players, one per line.")]
    public TMP_Text playerListText;

    [Tooltip("Label used per player. Use {0} for player number (1-based).")]
    public string playerLabelFormat = "Player {0}";

    private LobbyManager _lobbyManager;

    void Awake()
    {
        _lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (_lobbyManager == null)
            Debug.LogError("[LobbyPlayerList] No LobbyManager found in scene!");
    }

    void OnEnable()
    {
        if (_lobbyManager == null) return;
        _lobbyManager.OnPlayerJoined += _ => RefreshUI();
        _lobbyManager.OnPlayerLeft   += _ => RefreshUI();
        RefreshUI();
    }

    void OnDisable()
    {
        if (_lobbyManager == null) return;
        _lobbyManager.OnPlayerJoined -= _ => RefreshUI();
        _lobbyManager.OnPlayerLeft   -= _ => RefreshUI();
    }

    private void RefreshUI()
    {
        var lobby = _lobbyManager.Lobby;
        int current = lobby.players.Count;
        int max     = lobby.maxPlayers;

        if (playerCountText != null)
            playerCountText.text = $"({current}/{max})";

        if (playerListText != null)
        {
            if (current == 0)
            {
                playerListText.text = "No players yet...";
                return;
            }

            var sb = new StringBuilder();
            foreach (var player in lobby.players)
                sb.AppendLine(string.Format(playerLabelFormat, player.id + 1));

            playerListText.text = sb.ToString().TrimEnd();
        }
    }
}
