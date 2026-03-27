using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoundData
{

    public int roundIndex;
    public float roundSpeed;
    public int activeImages;
     public SequenceData sequenceData;

    
    public List<bool> clickResults = new List<bool>();

   
    public List<Sprite> correctSymbols = new List<Sprite>();
    public List<Sprite> incorrectSymbols = new List<Sprite>();

    
    public int scoreAfterRound;

    public RoundData()
    {
        sequenceData = new SequenceData();
    }

    public RoundData(
        int roundIndex,
        float roundSpeed,
        int activeImages,
        SequenceData sequenceData,
        List<bool> clickResults,
        List<Sprite> correctSymbols,
        List<Sprite> incorrectSymbols,
        int scoreAfterRound)
    {
        this.roundIndex = roundIndex;
        this.roundSpeed = roundSpeed;
        this.activeImages = activeImages;

  
        this.sequenceData = sequenceData;

        this.clickResults = clickResults != null ? new List<bool>(clickResults) : new List<bool>();
        this.correctSymbols = correctSymbols != null ? new List<Sprite>(correctSymbols) : new List<Sprite>();
        this.incorrectSymbols = incorrectSymbols != null ? new List<Sprite>(incorrectSymbols) : new List<Sprite>();

        this.scoreAfterRound = scoreAfterRound;
    }
}
