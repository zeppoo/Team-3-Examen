using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SymbolData
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

    public List<SymbolType> roundSymbols;
    public int activeSymbolIndex = 0;

    public SymbolType GetActiveSymbol()
    {
        int clampedIndex = Mathf.Clamp(activeSymbolIndex, 0, roundSymbols.Count - 1);
        return roundSymbols[clampedIndex];
    }

    [System.Serializable]
    [CreateAssetMenu(fileName = "SymbolType", menuName = "Scriptable Objects/SymbolType")]
    public class SymbolSpritePair : ScriptableObject
    {
        public SymbolData.SymbolType symbolType;
        public Sprite sprite;
    }
}
