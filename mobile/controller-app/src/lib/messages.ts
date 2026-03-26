// ─── Shared message schema ────────────────────────────────────────────────────
// Every message sent over the WebSocket has a `type` discriminator.
// Unity's ControllerMessages.cs must mirror these structures exactly.

// ─── QR code payload ─────────────────────────────────────────────────────────
// Encoded by Unity (LobbyDisplay.cs) and decoded by the mobile app on scan.
export interface LobbyInfo {
	ip: string;     // host LAN IP, e.g. "10.157.44.122"
	port: number;   // WebSocket port, e.g. 8080
	lobby: string;  // lobby name, e.g. "Test-Lobby"
}


// Sent when a drum pad is pressed or released.
export interface PadMessage {
	type: 'pad';
	id: 'pad1' | 'pad2';
	state: 'press' | 'release';
}

// Sent continuously while the scratch disc is being dragged.
export interface ScratchMessage {
	type: 'scratch';
	// Pixels per frame the disc moved; negative = backward, positive = forward.
	velocity: number;
}

// Union of all messages the controller can send.
export type ControllerMessage = PadMessage | ScratchMessage;

// ─── Helpers ──────────────────────────────────────────────────────────────────

export function padMessage(id: PadMessage['id'], state: PadMessage['state']): PadMessage {
	return { type: 'pad', id, state };
}

export function scratchMessage(velocity: number): ScratchMessage {
	return { type: 'scratch', velocity };
}
