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

    // ─── Server → Client messages ────────────────────────────────────────

    // Sent from Unity to the phone immediately after it joins.
    // {"type":"player_assigned","playerId":0,"color":"#E74C3C"}
    [Serializable]
    public class PlayerAssignedMessage
    {
        public string type    = "player_assigned";
        public int    playerId;
        public string color;  // hex, e.g. "#E74C3C"
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
