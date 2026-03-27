using System.Collections.Generic;
using UnityEngine;
using static SequenceData;

public class GameManager : MonoBehaviour
{
    #region Sequence / Symbol Data

    // Logical sequence for the current game/round
    [SerializeField] private SequenceData sequenceData = new SequenceData();

    [Header("Symbol → Sprite Mapping")]
    [SerializeField] private List<SymbolSpritePair> symbolSprites;

    // Runtime lookup from logical symbol type to actual Sprite
    private Dictionary<SequenceData.SymbolType, Sprite> spriteLookup;

    #endregion

    #region Score Data

    // Pure data holder for score; filled/used by ScoreCounter or other systems
    [SerializeField] private ScoreData scoreData = new ScoreData(0);

    public ScoreData ScoreData => scoreData;

    #endregion

    #region Round Data

    // History of all rounds played (each RoundData is data only)
    public List<RoundData> rounds = new List<RoundData>();

    public SequenceData SequenceData => sequenceData;

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
        spriteLookup = new Dictionary<SequenceData.SymbolType, Sprite>();

        foreach (var pair in symbolSprites)
        {
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

    public Sprite GetSprite(SequenceData.SymbolType type)
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

    // Example helpers – you can call these from SymbolSequenceMaker / elsewhere

    public void StartNewRound(int roundIndex, float roundSpeed, int activeImages)
    {
        RoundData newRound = new RoundData
        {
            roundIndex = roundIndex,
            roundSpeed = roundSpeed,
            activeImages = activeImages,
            sequenceData = sequenceData,
            scoreAfterRound = scoreData.score
        };

        rounds.Add(newRound);
    }

    public void FinishRound(List<bool> clickResults,
                            List<Sprite> correct,
                            List<Sprite> incorrect)
    {
        if (rounds.Count == 0) return;

        RoundData current = rounds[rounds.Count - 1];
        current.clickResults = new List<bool>(clickResults);
        current.correctSymbols = new List<Sprite>(correct);
        current.incorrectSymbols = new List<Sprite>(incorrect);
        current.scoreAfterRound = scoreData.score;
    }

    #endregion
}
