
using System;
using System.Collections.Generic;

[Serializable]
public class RoundData
{
    public float roundSpeed;
    public SymbolInstance[] sequence;
    public int activeSymbolIndex = 0;

    private static readonly Random _rng = new();
    private static readonly SymbolType[] _allSymbols = (SymbolType[])Enum.GetValues(typeof(SymbolType));

    public RoundData() { }

    /// <summary>
    /// Builds a random sequence and assigns each symbol to a player.
    /// Each player owns 2 symbol types; the sequence only contains symbols from those assignments.
    /// </summary>
    public RoundData(float speed, int sequenceLength, List<Player> players, HashSet<SymbolType> availableSymbols = null)
    {
        roundSpeed = speed;

        // Build pool of (symbolType, player) pairs
        // Instruments are unique per player, scratch types are shared by all
        var pool = new List<(SymbolType symbol, Player owner)>();
        foreach (var player in players)
        {
            pool.Add((player.button1Symbol, player));
            pool.Add((player.button2Symbol, player));
            if (availableSymbols == null || availableSymbols.Contains(SymbolType.ScratchPad_UP))
                pool.Add((SymbolType.ScratchPad_UP, player));
            if (availableSymbols == null || availableSymbols.Contains(SymbolType.ScratchPad_DOWN))
                pool.Add((SymbolType.ScratchPad_DOWN, player));
        }

        if (pool.Count == 0)
        {
            sequence = Array.Empty<SymbolInstance>();
            return;
        }

        sequence = new SymbolInstance[sequenceLength];
        for (int i = 0; i < sequenceLength; i++)
        {
            var (symbolType, owner) = pool[_rng.Next(pool.Count)];
            sequence[i] = new SymbolInstance
            {
                symbolType = symbolType,
                playerId   = owner.id,
                color      = UnityEngine.ColorUtility.TryParseHtmlString(owner.color, out var c) ? c : UnityEngine.Color.white
            };
        }
    }

    public SymbolInstance GetActiveSymbol()
    {
        int i = UnityEngine.Mathf.Clamp(activeSymbolIndex, 0, sequence.Length - 1);
        return sequence[i];
    }
}
