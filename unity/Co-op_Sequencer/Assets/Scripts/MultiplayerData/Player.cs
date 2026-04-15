using System;

[Serializable]
public class Player
{
    public int        id;
    public string     clientId;
    public string     color;         // hex, e.g. "#FF0000"
    public bool       connected = true;
    public SymbolType button1Symbol; // unique instrument
    public SymbolType button2Symbol; // unique instrument

    public Player(int id, string clientId, string color)
    {
        this.id        = id;
        this.clientId  = clientId;
        this.color     = color;
        this.connected = true;
    }
}
