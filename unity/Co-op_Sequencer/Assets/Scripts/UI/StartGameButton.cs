using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to a Button. When clicked, loads the game scene and carries the
/// lobby data across via a static reference on LobbyManager.
///
/// Required scene setup:
///   - GameObject with a Button component
///   - This script on the same (or any) GameObject
///   - Assign the Button to the `startButton` field (or wire it via OnClick)
///   - Set `gameSceneName` to the name of your game scene
///   - LobbyManager somewhere in the scene
/// </summary>
public class StartGameButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Button that triggers the scene load. If left empty, the Button on this GameObject is used.")]
    public Button startButton;

    [Header("Scene")]
    [Tooltip("Exact name of the game scene to load (must be added to Build Settings).")]
    public string gameSceneName = "GameMenu";

    [Tooltip("Minimum number of players required before the game can start. Set to 0 to allow starting with any number.")]
    public int minPlayers = 1;

    private LobbyManager _lobbyManager;

    void Awake()
    {
        _lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (_lobbyManager == null)
            Debug.LogError("[StartGameButton] No LobbyManager found in scene!");

        if (startButton == null)
            startButton = GetComponent<Button>();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
        else
            Debug.LogError("[StartGameButton] No Button found. Assign one or attach this script to a Button GameObject.");
    }

    void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
    }

    void Update()
    {
        // Keep the button interactable state in sync with the player count.
        if (startButton != null && _lobbyManager != null)
            startButton.interactable = _lobbyManager.Lobby.players.Count >= minPlayers;
    }

    private void OnStartClicked()
    {
        if (_lobbyManager == null)
        {
            Debug.LogError("[StartGameButton] Cannot start game: LobbyManager is missing.");
            return;
        }

        int count = _lobbyManager.Lobby.players.Count;
        if (count < minPlayers)
        {
            Debug.LogWarning($"[StartGameButton] Not enough players ({count}/{minPlayers}).");
            return;
        }

        Debug.Log($"[StartGameButton] Starting game with {count} player(s). Loading scene '{gameSceneName}'.");

        SceneManager.LoadScene(gameSceneName);
    }
}
