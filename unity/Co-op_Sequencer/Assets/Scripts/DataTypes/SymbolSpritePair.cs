using UnityEngine;

[CreateAssetMenu(fileName = "SymbolSpritePair", menuName = "Scriptable Objects/SymbolSpritePair")]
public class SymbolSpritePair : ScriptableObject
{
    public SymbolType symbolType;
    public Sprite sprite;
}
