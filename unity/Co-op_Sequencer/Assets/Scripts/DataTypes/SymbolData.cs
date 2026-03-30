using System.Collections.Generic;
using UnityEngine;
using static RoundData;

[System.Serializable]
public class SymbolData
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "SymbolType", menuName = "Scriptable Objects/SymbolType")]
    public class SymbolSpritePair : ScriptableObject
    {
        public RoundData.SymbolType symbolType;
        public Sprite sprite;
    }
}
