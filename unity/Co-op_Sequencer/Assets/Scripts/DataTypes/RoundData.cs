
using System;

[Serializable]
public class RoundData
{
    public int sequenceLength;

    public RoundData() { }

    public RoundData(int sequenceLength)
    {
        this.sequenceLength = sequenceLength;
    }
}
