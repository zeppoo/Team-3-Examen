namespace CoopSequencer.Networking
{
    using System;

    /// <summary>
    /// Message schemas sent between the mobile controller and the game host.
    /// Every field name must match the JSON keys exactly (camelCase).
    /// Mirrored in mobile/controller-app/src/lib/messages.ts.
    /// </summary>

    // QR code payload scanned by the mobile app to join the lobby.
    // {"ip":"10.0.0.1","port":8080,"lobby":"game"}
    [Serializable]
    public class LobbyInfo
    {
        public string ip;
        public int    port;
        public string lobby;
    }

    // Thin wrapper used to read the "type" field before full deserialization.
    [Serializable]
    public class MessageEnvelope
    {
        public string type;
    }

    // ─── Client → Server messages ────────────────────────────────────────

    // Legacy button message — kept for backward compatibility with SymbolScroller.
    // {"type":"button","button":"button1","player":"clientId","state":"press"}
    [Serializable]
    public class ButtonMessage
    {
        public string type;
        public string button;
        public string player;
        public string state;
    }

    // {"type":"scratch","player":"clientId","velocity":4.5}
    // velocity: pixels/frame, negative = backward
    [Serializable]
    public class ScratchMessage
    {
        public string type;    // always "scratch"
        public string player;  // clientId of the player who scratched
        public float  velocity;
    }

    // {"type":"slider","player":"clientId","direction":"up"}
    // direction: "up" | "down" — lane switch via the DJ fader
    [Serializable]
    public class SliderMessage
    {
        public string type;      // always "slider"
        public string player;    // clientId of the player
        public string direction; // "up" | "down"
    }

    // {"type":"symbol_select","player":"clientId","symbol":"Guitar","name":"Alex"}
    // Sent when a player claims an instrument symbol + submits their name.
    [Serializable]
    public class SymbolSelectMessage
    {
        public string type;    // always "symbol_select"
        public string player;  // clientId of the player
        public string symbol;  // "Guitar" | "Drums" | "Trumpet" | "Microphone"
        public string name;    // chosen display name
    }

    // {"type":"rejoin","lobbyId":"...","reconnectToken":"..."}
    // First message a phone sends if it has a stored cookie matching this lobby's id.
    [Serializable]
    public class RejoinMessage
    {
        public string type;            // always "rejoin"
        public string lobbyId;         // must match LobbyManager.LobbyId
        public string reconnectToken;  // token handed out in the previous player_assigned
    }

    // ─── Server → Client messages ────────────────────────────────────────

    // Sent from Unity to the phone immediately after it joins, and again
    // after a symbol is successfully claimed (with symbol + name set).
    // {"type":"player_assigned","playerId":0,"color":"#E74C3C","symbol":"Guitar","name":"Alex"}
    [Serializable]
    public class PlayerAssignedMessage
    {
        public string type    = "player_assigned";
        public int    playerId;
        public string color;          // hex, e.g. "#E74C3C"
        public string symbol;         // instrument symbol name (empty until claimed)
        public string name;           // chosen display name (empty until claimed)
        public string lobbyId;        // id of this lobby instance (for cookie matching)
        public string reconnectToken; // opaque token; phone stores it to rejoin this slot
    }

    // Broadcast to all clients when the set of available/taken symbols changes.
    // {"type":"symbols_update","available":["Guitar","Drums"],"taken":["Trumpet","Microphone"]}
    [Serializable]
    public class SymbolsUpdateMessage
    {
        public string   type      = "symbols_update";
        public string[] available;
        public string[] taken;
    }

    // Sent to a single client when their symbol_select was rejected.
    // {"type":"symbol_rejected","symbol":"Guitar","reason":"taken"}
    [Serializable]
    public class SymbolRejectedMessage
    {
        public string type   = "symbol_rejected";
        public string symbol;
        public string reason; // "taken" | other
    }

    // Sent from Unity when a player's lane changes.
    // {"type":"lane_update","playerId":0,"lane":2,"totalLanes":4}
    [Serializable]
    public class LaneUpdateMessage
    {
        public string type       = "lane_update";
        public int    playerId;
        public int    lane;       // current lane index (0-based)
        public int    totalLanes; // total number of lanes
    }

    // Sent from Unity to the phone when the player's score changes.
    // {"type":"score_update","playerId":0,"score":350,"lastHitPoints":100,"rating":"perfect"}
    [Serializable]
    public class ScoreUpdateMessage
    {
        public string type          = "score_update";
        public int    playerId;
        public int    score;         // cumulative score
        public int    lastHitPoints; // points from the most recent hit (0 = miss)
        public string rating;       // "perfect", "good", "ok", or "miss"
    }
}
