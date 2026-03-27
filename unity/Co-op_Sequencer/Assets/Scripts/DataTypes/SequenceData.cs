using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SequenceData
{
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

    public List<SymbolType> symbols;
    public int activeSymbolIndex = 0;

    public SymbolType GetActiveSymbol()
    {
        int clampedIndex = Mathf.Clamp(activeSymbolIndex, 0, symbols.Count - 1);
        return symbols[clampedIndex];
    }

    [System.Serializable]
    public class SymbolSpritePair
    {
        public SequenceData.SymbolType symbolType;
        public Sprite sprite;
    }
}
