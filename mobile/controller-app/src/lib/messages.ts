// ─── Shared message schema ────────────────────────────────────────────────────
// Every message sent over the WebSocket has a `type` discriminator.
// Unity's ControllerMessages.cs must mirror these structures exactly.

// ─── QR code payload ─────────────────────────────────────────────────────────
// Encoded by Unity (LobbyDisplay.cs) and decoded by the mobile app on scan.
export interface LobbyInfo {
	ip: string;      // host LAN IP, e.g. "10.157.44.122"
	port: number;    // WebSocket port, e.g. 8080
	lobby: string;   // lobby name, e.g. "Test-Lobby"
	lobbyId?: string; // unique per-boot id, used for reconnect cookies
}

// Sent as the first message after (re)connecting when the phone has a stored
// cookie matching this lobby's id. Unity looks up the token and restores the
// player's slot (id, color, symbol, name).
// {"type":"rejoin","lobbyId":"...","reconnectToken":"..."}
export interface RejoinMessage {
	type: 'rejoin';
	lobbyId: string;
	reconnectToken: string;
}

export function rejoinMessage(lobbyId: string, reconnectToken: string): RejoinMessage {
	return { type: 'rejoin', lobbyId, reconnectToken };
}


// Sent continuously while the scratch disc is being dragged (main gameplay input).
// {"type":"scratch","player":"<clientId>","velocity":4.5}
export interface ScratchMessage {
	type: 'scratch';
	player: string;
	// Pixels per frame the disc moved; negative = backward, positive = forward.
	velocity: number;
}

// Sent when the player slides the DJ fader to switch lane.
// {"type":"slider","player":"<clientId>","direction":"up"|"down"}
export interface SliderMessage {
	type: 'slider';
	player: string;
	direction: 'up' | 'down';
}

// Instrument symbols the player can pick.
export type SymbolType = 'Guitar' | 'Drums' | 'Trumpet' | 'Microphone';
export const ALL_SYMBOLS: SymbolType[] = ['Guitar', 'Drums', 'Trumpet', 'Microphone'];

// Sent when the player tries to claim a symbol + submit their name.
// {"type":"symbol_select","player":"<clientId>","symbol":"Guitar","name":"Alex"}
export interface SymbolSelectMessage {
	type: 'symbol_select';
	player: string;
	symbol: SymbolType;
	name: string;
}

// Union of all messages the controller can send.
export type ControllerMessage = ScratchMessage | SliderMessage | SymbolSelectMessage | RejoinMessage;

// ─── Server → client messages ─────────────────────────────────────────────────

// Sent by the server when something goes wrong (e.g. lobby full).
export interface ErrorMessage {
	type: 'error';
	reason: 'lobby_full' | string;
}

// Sent by Unity immediately after a player joins, assigning their id and color.
// {"type":"player_assigned","playerId":0,"color":"#fe0000","symbol":"Guitar","name":"Alex"}
export interface PlayerAssignedMessage {
	type: 'player_assigned';
	playerId: number;
	color: string;           // hex, e.g. "#fe0000"
	symbol?: SymbolType;     // set once the player has confirmed their pick
	name?: string;           // player's chosen display name
	lobbyId?: string;        // id of the lobby this assignment belongs to
	reconnectToken?: string; // token the phone stores to rejoin this slot
}

// Broadcast by Unity whenever the set of available (unclaimed) symbols changes.
// {"type":"symbols_update","available":["Guitar","Drums"],"taken":["Trumpet","Microphone"]}
export interface SymbolsUpdateMessage {
	type: 'symbols_update';
	available: SymbolType[];
	taken: SymbolType[];
}

// Sent by Unity if a symbol claim was rejected (someone else got there first).
// {"type":"symbol_rejected","symbol":"Guitar","reason":"taken"}
export interface SymbolRejectedMessage {
	type: 'symbol_rejected';
	symbol: SymbolType;
	reason: string;
}

// Sent by Unity when a player's lane changes.
// {"type":"lane_update","playerId":0,"lane":2,"totalLanes":4}
export interface LaneUpdateMessage {
	type: 'lane_update';
	playerId: number;
	lane: number;           // current lane index (0-based)
	totalLanes: number;     // total number of lanes
}

// Sent by Unity when the player's score changes after a hit or miss.
// {"type":"score_update","playerId":0,"score":350,"lastHitPoints":100,"rating":"perfect"}
export interface ScoreUpdateMessage {
	type: 'score_update';
	playerId: number;
	score: number;          // cumulative score
	lastHitPoints: number;  // points from last hit (0 = miss)
	rating: 'perfect' | 'good' | 'ok' | 'miss';
}

export type ServerMessage = ErrorMessage | PlayerAssignedMessage | LaneUpdateMessage | ScoreUpdateMessage | SymbolsUpdateMessage | SymbolRejectedMessage;

// ─── Helpers ──────────────────────────────────────────────────────────────────

export function scratchMessage(velocity: number, player: string): ScratchMessage {
	return { type: 'scratch', player, velocity };
}

export function sliderMessage(direction: SliderMessage['direction'], player: string): SliderMessage {
	return { type: 'slider', player, direction };
}

export function symbolSelectMessage(symbol: SymbolType, name: string, player: string): SymbolSelectMessage {
	return { type: 'symbol_select', player, symbol, name };
}
