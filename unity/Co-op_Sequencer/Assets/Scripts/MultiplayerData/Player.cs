using System;

[Serializable]
public class Player
{
    public int        id;
    public string     clientId;
    public string     color;         // hex, e.g. "#FF0000"
    public bool       connected = true;
    public int        lane;          // current lane index (0-based)

    public Player(int id, string clientId, string color)
    {
        this.id        = id;
        this.clientId  = clientId;
        this.color     = color;
        this.connected = true;
        this.lane      = id; // default: each player starts on their own lane
    }
}
