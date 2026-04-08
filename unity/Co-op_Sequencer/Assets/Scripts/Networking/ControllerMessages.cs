namespace CoopSequencer.Networking
{
    using System;

    /// <summary>
    /// Message schemas sent from the mobile controller to the game host.
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

    // {"type":"button","button":"button1","player":"clientId","state":"press"}
    // state: "press" | "release"
    [Serializable]
    public class ButtonMessage
    {
        public string type;    // always "button"
        public string button;  // e.g. "button1", "button2"
        public string player;  // clientId of the player who pressed it
        public string state;   // "press" | "release"
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

    // Sent from Unity to the phone immediately after it joins, and again when symbols are assigned.
    // {"type":"player_assigned","playerId":0,"color":"#E74C3C","button1Symbol":"Square","button1Image":"<base64>","button2Symbol":"Heart","button2Image":"<base64>"}
    [Serializable]
    public class PlayerAssignedMessage
    {
        public string type           = "player_assigned";
        public int    playerId;
        public string color;          // hex, e.g. "#E74C3C"
        public string button1Symbol;  // unique instrument
        public string button1Image;
        public string button2Symbol;  // unique instrument
        public string button2Image;
    }
}
