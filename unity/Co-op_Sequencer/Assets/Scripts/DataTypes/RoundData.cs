using System.Collections.Generic;
using UnityEngine;

[System.Serializable]

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
public class RoundData
{
    public int roundIndex;  
    public float roundSpeed;
    public List<SymbolType> roundSymbols;
    public List<RoundData> rounds = new List<RoundData>();
    public int activeSymbolIndex = 0;

    public SymbolType GetActiveSymbol()
    {
        int clampedIndex = Mathf.Clamp(activeSymbolIndex, 0, roundSymbols.Count - 1);
        return roundSymbols[clampedIndex];
    }


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
