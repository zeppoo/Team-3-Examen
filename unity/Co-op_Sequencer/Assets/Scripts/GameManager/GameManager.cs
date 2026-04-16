using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Sequence / Symbol Data

    [Header("Symbol Sprites (ScratchPad_UP, ScratchPad_DOWN)")]
    [SerializeField] private List<SymbolSpritePair> symbolSprites;

    [Header("Player Icon Sprites (instrument icons assigned to players)")]
    [SerializeField] private List<Sprite> playerIconSprites;

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

        // Downscale to max 128px for mobile (keeps message small for Cloudflare tunnel)
        const int maxSize = 128;
        if (tex.width > maxSize || tex.height > maxSize)
        {
            float scale = Mathf.Min((float)maxSize / tex.width, (float)maxSize / tex.height);
            int newW = Mathf.Max(1, Mathf.RoundToInt(tex.width * scale));
            int newH = Mathf.Max(1, Mathf.RoundToInt(tex.height * scale));

            var rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            small.ReadPixels(new UnityEngine.Rect(0, 0, newW, newH), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Object.Destroy(tex);
            tex = small;
        }

        int finalW = tex.width, finalH = tex.height;
        var png = ImageConversion.EncodeToPNG(tex);
        Object.Destroy(tex);
        Debug.Log($"[GameManager] SpriteToBase64({type}): {finalW}x{finalH} → {png.Length} bytes");
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

    /// <summary>Returns the icon sprite assigned to a player (by index).</summary>
    public Sprite GetPlayerIconSprite(int playerIndex)
    {
        if (playerIconSprites == null || playerIconSprites.Count == 0) return null;
        return playerIconSprites[playerIndex % playerIconSprites.Count];
    }

    #endregion

    #region Round Methods

    /// <summary>
    /// Assigns player icon sprites and creates a new round.
    /// </summary>
    public void StartNewRound(int sequenceLength, List<Player> players)
    {
        if (players.Count == 0)
        {
            Debug.LogError("[GameManager] No players in lobby — cannot start round.");
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"[GameManager] Player {players[i].id}: icon={GetPlayerIconSprite(i)?.name ?? "none"}");
        }

        rounds.Add(new RoundData(sequenceLength));
    }

    /// <summary>
    /// Generates sequences for all lanes at once. Each beat spawns exactly
    /// playerCount symbols spread across totalLanes (= playerCount + 1),
    /// so one lane is always empty per beat. The empty lane varies each beat.
    /// Returns an array of sequences, one per lane. Null entries = no symbol on that beat.
    /// </summary>
    /// <param name="spawnChance">Chance (0-1) that symbols spawn on any given beat. Beats that fail the roll are fully empty (breather).</param>
    public SymbolInstance[][] GenerateAllLaneSequences(int sequenceLength, int totalLanes, int playerCount, float spawnChance = 1f)
    {
        var symbols = new List<SymbolType>();
        if (spriteLookup.ContainsKey(SymbolType.ScratchPad_UP))
            symbols.Add(SymbolType.ScratchPad_UP);
        if (spriteLookup.ContainsKey(SymbolType.ScratchPad_DOWN))
            symbols.Add(SymbolType.ScratchPad_DOWN);

        if (symbols.Count == 0)
        {
            Debug.LogError("[GameManager] No scratch symbol sprites configured!");
            return System.Array.Empty<SymbolInstance[]>();
        }

        var sequences = new SymbolInstance[totalLanes][];
        for (int lane = 0; lane < totalLanes; lane++)
            sequences[lane] = new SymbolInstance[sequenceLength];

        for (int beat = 0; beat < sequenceLength; beat++)
        {
            // Roll spawn chance — if it fails, entire beat is empty (breather)
            if (Random.value > spawnChance)
                continue; // all lanes stay null for this beat

            int emptyLane = Random.Range(0, totalLanes);

            for (int lane = 0; lane < totalLanes; lane++)
            {
                if (lane == emptyLane)
                {
                    sequences[lane][beat] = null;
                }
                else
                {
                    var symbolType = symbols[Random.Range(0, symbols.Count)];
                    sequences[lane][beat] = new SymbolInstance
                    {
                        symbolType = symbolType,
                        playerId   = -1,
                        color      = Color.white
                    };
                }
            }
        }

        return sequences;
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
