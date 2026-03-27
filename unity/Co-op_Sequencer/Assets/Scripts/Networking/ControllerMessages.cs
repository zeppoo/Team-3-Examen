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
}
