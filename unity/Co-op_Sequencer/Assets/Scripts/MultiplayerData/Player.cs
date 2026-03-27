using System;

[Serializable]
public class Player
{
    public int    id;
    public string clientId;

    public Player(int id, string clientId)
    {
        this.id       = id;
        this.clientId = clientId;
    }
}
