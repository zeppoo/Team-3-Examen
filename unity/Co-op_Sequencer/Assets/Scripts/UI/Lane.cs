using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component attached to each Lane prefab instance at runtime.
/// Manages player icons and the per-lane SymbolScroller.
///
/// Expected prefab child structure:
///   Lane
///   ├── PlayerIcons_PNL  — narrow left strip for colored player icons
///   └── ScrollArea_PNL   — the rest of the lane width
///       └── TimingZone   — small zone on the left side of ScrollArea
///
/// Do NOT add a HorizontalLayoutGroup to Lane — this script positions
/// PlayerIcons and ScrollArea manually to avoid layout conflicts.
/// </summary>
public class Lane : MonoBehaviour
{
    private float minPlayerIconsWidth = 40f;
    private float maxPlayerIconsWidth = 80f;
    private GameObject _playerIconPrefab;

    public int LaneIndex { get; set; }

    public void SetPlayerIconsWidthRange(float min, float max)
    {
        minPlayerIconsWidth = min;
        maxPlayerIconsWidth = max;
    }

    public void SetPlayerIconPrefab(GameObject prefab)
    {
        _playerIconPrefab = prefab;
    }
    public SymbolScroller Scroller { get; private set; }

    private RectTransform _rect;
    private RectTransform _playerIconsParent;
    private RectTransform _scrollArea;
    private RectTransform _timingZone;
    private RectTransform _symbolContainer;

    private readonly List<GameObject> _playerIcons = new();

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _playerIconsParent = transform.Find("PlayerIcons_PNL")?.GetComponent<RectTransform>();
        _scrollArea        = transform.Find("ScrollArea_PNL")?.GetComponent<RectTransform>();

        if (_scrollArea != null)
            _timingZone = _scrollArea.Find("TimingZone")?.GetComponent<RectTransform>();

        // Create symbol container inside ScrollArea, isolated from layout
        if (_scrollArea != null)
        {
            var containerGO = new GameObject("SymbolContainer");
            _symbolContainer = containerGO.AddComponent<RectTransform>();
            _symbolContainer.SetParent(_scrollArea, false);
            _symbolContainer.anchorMin = Vector2.zero;
            _symbolContainer.anchorMax = Vector2.one;
            _symbolContainer.offsetMin = Vector2.zero;
            _symbolContainer.offsetMax = Vector2.zero;
        }

        if (_playerIconsParent == null) Debug.LogWarning($"[Lane] PlayerIcons_PNL not found on {name}");
        if (_scrollArea == null)        Debug.LogWarning($"[Lane] ScrollArea_PNL not found on {name}");
        if (_timingZone == null)        Debug.LogWarning($"[Lane] TimingZone not found on {name}");
    }

    private void SetupLayout()
    {
        // Scale player icons width with lane height, clamped
        float laneHeight = _rect != null ? _rect.rect.height : 100f;
        float iconsWidth = Mathf.Clamp(laneHeight, minPlayerIconsWidth, maxPlayerIconsWidth);

        // PlayerIcons: anchored to the left, fixed width
        if (_playerIconsParent != null)
        {
            _playerIconsParent.anchorMin = new Vector2(0, 0);
            _playerIconsParent.anchorMax = new Vector2(0, 1);
            _playerIconsParent.pivot     = new Vector2(0, 0.5f);
            _playerIconsParent.offsetMin = new Vector2(0, 0);
            _playerIconsParent.offsetMax = new Vector2(iconsWidth, 0);

            // Add a layout group so icons stack vertically and are centered
            var vlg = _playerIconsParent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = _playerIconsParent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
        }

        // ScrollArea: fills the rest of the lane to the right of PlayerIcons
        if (_scrollArea != null)
        {
            _scrollArea.anchorMin = new Vector2(0, 0);
            _scrollArea.anchorMax = new Vector2(1, 1);
            _scrollArea.pivot     = new Vector2(0.5f, 0.5f);
            _scrollArea.offsetMin = new Vector2(iconsWidth, 0);
            _scrollArea.offsetMax = new Vector2(0, 0);
        }
    }

    public void SetupScroller(GameObject symbolPrefab, GameManager gameManager, AudioManager audioManager)
    {
        SetupLayout();
        Scroller = gameObject.AddComponent<SymbolScroller>();
        Scroller.LaneIndex = LaneIndex;
        Scroller.Initialize(_scrollArea, _symbolContainer, _timingZone, symbolPrefab, gameManager, audioManager);
    }

    public void SetPlayers(List<Player> players, GameManager gameManager = null)
    {
        ClearPlayerIcons();

        if (_playerIconsParent == null)
        {
            Debug.LogWarning($"[Lane {LaneIndex}] PlayerIcons_PNL is null, cannot set player icons");
            return;
        }

        if (_playerIconPrefab == null)
        {
            Debug.LogWarning($"[Lane {LaneIndex}] No player icon prefab assigned");
            return;
        }

        foreach (var player in players)
        {
            var iconGO = Instantiate(_playerIconPrefab, _playerIconsParent);
            iconGO.name = $"PlayerIcon_{player.id}";

            var img = iconGO.GetComponent<Image>();
            if (img == null) img = iconGO.GetComponentInChildren<Image>();

            if (img != null)
            {
                var iconSprite = gameManager?.GetPlayerIconSprite(player.id);
                if (iconSprite != null) img.sprite = iconSprite;

                if (ColorUtility.TryParseHtmlString(player.color, out var color))
                    img.color = color;
            }

            _playerIcons.Add(iconGO);
            Debug.Log($"[Lane {LaneIndex}] Added player icon for player {player.id} (color={player.color})");
        }
    }

    public void ClearPlayerIcons()
    {
        foreach (var go in _playerIcons)
            if (go != null) Destroy(go);
        _playerIcons.Clear();
    }
}
