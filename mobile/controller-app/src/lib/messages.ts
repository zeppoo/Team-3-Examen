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
// {"type":"button","button":"button1","state":"press"}
export interface ButtonMessage {
	type: 'button';
	button: 'button1' | 'button2';
	state: 'press' | 'release';
}

// Sent continuously while the scratch disc is being dragged.
// {"type":"scratch","velocity":4.5}
export interface ScratchMessage {
	type: 'scratch';
	// Pixels per frame the disc moved; negative = backward, positive = forward.
	velocity: number;
}

// Union of all messages the controller can send.
export type ControllerMessage = ButtonMessage | ScratchMessage;

// ─── Server → client messages ─────────────────────────────────────────────────

// Sent by the server when something goes wrong (e.g. lobby full).
export interface ErrorMessage {
	type: 'error';
	reason: 'lobby_full' | string;
}

export type ServerMessage = ErrorMessage;

// ─── Helpers ──────────────────────────────────────────────────────────────────

export function buttonMessage(button: ButtonMessage['button'], state: ButtonMessage['state']): ButtonMessage {
	return { type: 'button', button, state };
}

export function scratchMessage(velocity: number): ScratchMessage {
	return { type: 'scratch', velocity };
}
