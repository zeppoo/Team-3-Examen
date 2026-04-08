using System;
using UnityEngine;

[Serializable]
public enum SymbolType
{
    ScratchPad_UP,
    ScratchPad_DOWN,
    Keys,
    Drums,
    Saxophone,
    Guitar,
    Trumpet,
    Microphone,
}

// Runtime instance: a symbol in the active sequence with its owning player resolved.
[System.Serializable]
public class SymbolInstance
{
    public SymbolType symbolType;
    public int        playerId; // which player must respond to this symbol
    public Color      color;    // player color, used for visual feedback in-game
}
