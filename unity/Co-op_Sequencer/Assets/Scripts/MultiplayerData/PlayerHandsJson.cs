using System;
using System.Collections.Generic;

[Serializable]
public class PlayerHand
{
    public List<string> cards;
}

[Serializable]
public class PlayerHandsJson
{
    public List<PlayerHand> playerHandss;
}
