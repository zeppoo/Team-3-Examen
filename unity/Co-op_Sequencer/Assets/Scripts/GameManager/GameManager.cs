using System.Collections.Generic;
using UnityEngine;
using static SymbolData;

public class GameManager : MonoBehaviour
{
    #region Sequence / Symbol Data

    // Logical sequence for the current game/round
    [SerializeField] private RoundData sequenceData = new RoundData();

    [Header("Symbol → Sprite Mapping")]
    [SerializeField] private List<SymbolSpritePair> symbolSprites;

    // Runtime lookup from logical symbol type to actual Sprite
    private Dictionary<SymbolType, Sprite> spriteLookup;

    public RoundData SymbolData => sequenceData;

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

    private void Start()
    {
        LogActiveSymbol();
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

    private void LogActiveSymbol()
    {
        var activeSymbol = sequenceData.GetActiveSymbol();
        Debug.Log("GameManager active symbol at start: " + activeSymbol);
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

    // Call this when a new round starts
    public void StartNewRound(int roundIndex, float roundSpeed, int activeImages)
    {
        RoundData newRound = new RoundData
        {
            roundIndex   = roundIndex,
            roundSpeed   = roundSpeed,
        };

        rounds.Add(newRound);
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
