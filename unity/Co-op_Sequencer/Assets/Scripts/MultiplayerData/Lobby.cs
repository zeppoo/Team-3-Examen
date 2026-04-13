using System;
using System.Collections.Generic;

[Serializable]
public class Lobby
{
    public string lobbyName;
    public int maxPlayers;
    public List<Player> players = new List<Player>();

    // One color per player slot — indices match player.id
    private static readonly string[] PlayerColors = { "#fe0000", "#6fb9f8", "#79bf00", "#faff02" };

    public Lobby(string lobbyName, int maxPlayers = 4)
    {
        this.lobbyName  = lobbyName;
        this.maxPlayers = maxPlayers;
    }

    public bool IsFull => players.Count >= maxPlayers;

    public Player AddPlayer(string clientId)
    {
        if (IsFull) return null;
        int index  = players.Count;
        string color = index < PlayerColors.Length ? PlayerColors[index] : "#FFFFFF";
        var player = new Player(index, clientId, color);
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

    public Player GetPlayerById(int id)
    {
        return players.Find(p => p.id == id);
    }

    public Player GetFirstDisconnected()
    {
        return players.Find(p => !p.connected);
    }
}
