using System.Collections.Generic;
using UnityEngine;

[System.Serializable]


public class RoundData
{
    public int roundIndex;  
    public float roundSpeed;
    public enum SymbolType
    {
        Square,
        Triangle,
        Circle,
        Peace,
        Hexagon,
        Nepal,
        Star,
        Plus,
        Minus,
        Heart
    }

    public SymbolType GetActiveSymbol()
    {
        int clampedIndex = Mathf.Clamp(activeSymbolIndex, 0, roundSymbols.Count - 1);
        return roundSymbols[clampedIndex];
    }

    public List<SymbolType> roundSymbols;
    public int activeSymbolIndex = 0;

    public List<RoundData> rounds = new List<RoundData>();

    public void CreateRound(int index, float speed, int activeImages, SymbolData sequence)
    {
        RoundData newRound = new RoundData
        {
            roundIndex = index,
            roundSpeed = speed,
        };
        rounds.Add(newRound);
    }
}
