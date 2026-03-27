using System;
using System.Collections.Generic;

[Serializable]
public class Lobby
{
    public string lobbyName;
    public int maxPlayers;
    public List<Player> players = new List<Player>();

    public Lobby(string lobbyName, int maxPlayers = 4)
    {
        this.lobbyName  = lobbyName;
        this.maxPlayers = maxPlayers;
    }

    public bool IsFull => players.Count >= maxPlayers;

    public Player AddPlayer(string clientId)
    {
        if (IsFull) return null;
        var player = new Player(players.Count, clientId);
        players.Add(player);
        return player;
    }

    public bool RemovePlayer(string clientId)
    {
        return players.RemoveAll(p => p.clientId == clientId) > 0;
    }

    public Player GetPlayer(string clientId)
    {
        return players.Find(p => p.clientId == clientId);
    }
}
