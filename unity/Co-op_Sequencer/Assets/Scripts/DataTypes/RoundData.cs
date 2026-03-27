using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoundData
{

    public int roundIndex;
    public float roundSpeed;

}


public class RoundDataList
{
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
