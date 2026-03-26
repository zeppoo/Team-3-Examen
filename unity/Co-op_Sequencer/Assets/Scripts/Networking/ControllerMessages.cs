namespace CoopSequencer.Networking
{
    using System;

    /// <summary>
    /// Mirror of the TypeScript message schema in mobile/controller-app/src/lib/messages.ts.
    /// Every field name must match the JSON keys exactly (camelCase).
    /// </summary>

    // QR code payload encoded by LobbyDisplay and scanned by the mobile app.
    // {"ip":"10.157.44.122","port":8080,"lobby":"Test-Lobby"}
    [Serializable]
    public class LobbyInfo
    {
        public string ip;     // host LAN IP
        public int    port;   // WebSocket port
        public string lobby;  // lobby name
    }

    // {"type":"pad","id":"pad1","state":"press"}
    [Serializable]
    public class PadMessage
    {
        public string type;   // always "pad"
        public string id;     // "pad1" | "pad2"
        public string state;  // "press" | "release"
    }

    // {"type":"scratch","velocity":4.5}
    [Serializable]
    public class ScratchMessage
    {
        public string type;    // always "scratch"
        public float velocity; // pixels/frame, negative = backward
    }

    // Thin wrapper used only to read the "type" field before full deserialisation.
    [Serializable]
    public class MessageEnvelope
    {
        public string type;
    }
}
