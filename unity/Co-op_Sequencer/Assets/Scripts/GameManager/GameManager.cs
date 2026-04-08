using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Sequence / Symbol Data

    [Header("Symbol → Sprite Mapping")]
    [SerializeField] private List<SymbolSpritePair> symbolSprites;

    // Runtime lookup from logical symbol type to actual Sprite
    private Dictionary<SymbolType, Sprite> spriteLookup;

    public RoundData CurrentRound => rounds.Count > 0 ? rounds[rounds.Count - 1] : null;

    #endregion

    #region Score Data

    // Pure data holder for score; filled/used by ScoreCounter or other systems
    [SerializeField] private ScoreData scoreData = new ScoreData(0);

    public ScoreData ScoreData => scoreData;

    #endregion

    #region Round Data

    // History of all rounds played (each RoundData is data only)
    public List<RoundData> rounds = new List<RoundData>();

    #endregion

    private void Awake()
    {
        InitializeSpriteLookup();
    }


    #region Sequence / Symbol Methods

    private void InitializeSpriteLookup()
    {
        spriteLookup = new Dictionary<SymbolType, Sprite>();

        foreach (var pair in symbolSprites)
        {
            if (pair == null) continue;

            if (!spriteLookup.ContainsKey(pair.symbolType))
            {
                spriteLookup.Add(pair.symbolType, pair.sprite);
            }
        }
    }

    public string SpriteToBase64(SymbolType type)
    {
        var sprite = GetSprite(type);
        if (sprite == null) return null;

        // Copy the sprite region into a readable Texture2D
        var src = sprite.texture;
        var rect = sprite.textureRect;
        var tex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        tex.SetPixels(src.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
        tex.Apply();

        var png = ImageConversion.EncodeToPNG(tex);
        Object.Destroy(tex);
        return System.Convert.ToBase64String(png);
    }

    public Sprite GetSprite(SymbolType type)
    {
        if (spriteLookup == null)
        {
            Debug.LogWarning("Sprite lookup not initialized. Initializing now.");
            InitializeSpriteLookup();
        }

        if (spriteLookup.TryGetValue(type, out Sprite sprite))
            return sprite;

        Debug.LogError("No sprite found for symbol: " + type);
        return null;
    }

    #endregion

    #region Round Methods

    /// <summary>
    /// Assigns 2 symbols per player then builds the round sequence from those assignments.
    /// </summary>
    public void StartNewRound(float roundSpeed, int sequenceLength, List<Player> players)
    {
        if (players.Count == 0)
        {
            Debug.LogError("[GameManager] No players in lobby — cannot start round.");
            return;
        }

        // Build a shuffled pool of instrument symbols that have sprites configured
        var instruments = new List<SymbolType>();
        foreach (SymbolType s in System.Enum.GetValues(typeof(SymbolType)))
            if (s != SymbolType.ScratchPad_UP && s != SymbolType.ScratchPad_DOWN
                && spriteLookup.ContainsKey(s))
                instruments.Add(s);

        for (int i = instruments.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (instruments[i], instruments[j]) = (instruments[j], instruments[i]);
        }

        if (instruments.Count < players.Count * 2)
        {
            Debug.LogError($"[GameManager] Not enough instrument types ({instruments.Count}) for {players.Count} players (need {players.Count * 2}).");
            return;
        }

        // Every player gets 2 unique instruments on buttons, scratch is shared by all
        for (int i = 0; i < players.Count; i++)
        {
            players[i].button1Symbol = instruments[i * 2];
            players[i].button2Symbol = instruments[i * 2 + 1];
            Debug.Log($"[GameManager] Player {players[i].id}: button1={players[i].button1Symbol} button2={players[i].button2Symbol}");
        }

        var available = new HashSet<SymbolType>(spriteLookup.Keys);
        rounds.Add(new RoundData(roundSpeed, sequenceLength, players, available));
    }

    // Call this when the round ends to store its results
    public void FinishRound(List<bool> clickResults,
                            List<Sprite> correct,
                            List<Sprite> incorrect)
    {
        if (rounds.Count == 0) return;

        RoundData current = rounds[rounds.Count - 1];
    }

    #endregion
}
