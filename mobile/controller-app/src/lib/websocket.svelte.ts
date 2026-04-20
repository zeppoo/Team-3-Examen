import { logger } from './logger.svelte';
import type { ControllerMessage, ServerMessage, SymbolType } from './messages';
import { ALL_SYMBOLS, rejoinMessage } from './messages';

// ── Reconnect cookie (1h TTL) ────────────────────────────────────────────
// Keyed per lobbyId so scanning a different lobby doesn't try to reuse a token.
const COOKIE_KEY = 'controller-reconnect-v1';
const COOKIE_TTL_MS = 60 * 60 * 1000; // 1 hour

interface ReconnectCookie {
	lobbyId: string;
	reconnectToken: string;
	expiresAt: number; // epoch ms
}

function loadCookie(lobbyId: string): ReconnectCookie | null {
	if (typeof localStorage === 'undefined') return null;
	try {
		const raw = localStorage.getItem(COOKIE_KEY);
		if (!raw) return null;
		const c = JSON.parse(raw) as ReconnectCookie;
		if (c.lobbyId !== lobbyId) return null;
		if (Date.now() > c.expiresAt) {
			localStorage.removeItem(COOKIE_KEY);
			return null;
		}
		return c;
	} catch {
		return null;
	}
}

function saveCookie(c: ReconnectCookie) {
	if (typeof localStorage === 'undefined') return;
	try { localStorage.setItem(COOKIE_KEY, JSON.stringify(c)); } catch { /* ignore */ }
}

function clearCookie() {
	if (typeof localStorage === 'undefined') return;
	try { localStorage.removeItem(COOKIE_KEY); } catch { /* ignore */ }
}

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

interface WebSocketStore {
	readonly status: ConnectionStatus;
	readonly lobbyInfo: { ip: string; port: number; lobby: string; lobbyId?: string } | null;
	readonly lastError: ServerMessage | null;
	readonly playerId: string | null;
	readonly playerColor: string | null;
	readonly lane: number;
	readonly totalLanes: number;
	readonly score: number;
	readonly lastHitPoints: number | null;
	readonly lastRating: string | null;
	readonly availableSymbols: SymbolType[];
	readonly takenSymbols: SymbolType[];
	readonly playerSymbol: SymbolType | null;
	readonly playerName: string;
	readonly lastRejectedSymbol: SymbolType | null;
	readonly didRejoin: boolean;
	setPlayerName(name: string): void;
	clearRejectedSymbol(): void;
	connect(ip: string, port: number, lobby: string, lobbyId?: string): Promise<boolean>;
	send(msg: ControllerMessage): void;
	disconnect(): void;
	bypassForTesting(): void;
}

function createWebSocketStore(): WebSocketStore {
	let socket: WebSocket | null = null;

	let status = $state<ConnectionStatus>('disconnected');
	let lobbyInfo = $state<{ ip: string; port: number; lobby: string; lobbyId?: string } | null>(null);
	let lastError = $state<ServerMessage | null>(null);
	let didRejoin = $state(false);
	let pendingRejoinToken: string | null = null;
	let playerId = $state<string | null>(null);
	let playerColor = $state<string | null>(null);
	let lane = $state<number>(0);
	let totalLanes = $state<number>(3);
	let score = $state<number>(0);
	let lastHitPoints = $state<number | null>(null);
	let lastRating = $state<string | null>(null);
	let availableSymbols = $state<SymbolType[]>([...ALL_SYMBOLS]);
	let takenSymbols = $state<SymbolType[]>([]);
	let playerSymbol = $state<SymbolType | null>(null);
	let playerName = $state<string>('');
	let lastRejectedSymbol = $state<SymbolType | null>(null);

	let intentionalClose = false;
	let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
	const MAX_RECONNECT_ATTEMPTS = 5;
	const RECONNECT_DELAY_MS = 1500;
	let reconnectAttempts = 0;

	async function waitForTunnel(host: string, maxAttempts = 10, intervalMs = 500): Promise<boolean> {
		const url = `https://${host}`;
		for (let i = 1; i <= maxAttempts; i++) {
			try {
				await fetch(url, { method: 'HEAD', mode: 'no-cors', signal: AbortSignal.timeout(2000) });
				logger.net(`Tunnel reachable after ${i} attempt(s)`);
				return true;
			} catch {
				logger.net(`Tunnel not ready yet (attempt ${i}/${maxAttempts}), retrying...`);
				await new Promise(r => setTimeout(r, intervalMs));
			}
		}
		logger.error(`Tunnel did not become reachable after ${maxAttempts} attempts`);
		return false;
	}

	let _connectResolve: ((ok: boolean) => void) | null = null;

	async function connect(ip: string, port: number, lobby: string, lobbyId?: string, isReconnect = false): Promise<boolean> {
		if (socket) {
			// Detach handlers so the old socket's onclose doesn't trigger reconnect
			socket.onclose = null;
			socket.onerror = null;
			socket.onmessage = null;
			logger.net(`Closing existing socket before reconnect`);
			socket.close();
			socket = null;
		}

		intentionalClose = false;
		if (!isReconnect) reconnectAttempts = 0;
		status = 'connecting';
		lobbyInfo = { ip, port, lobby, lobbyId };

		// If we have a stored token for this lobbyId, try to rejoin on open.
		pendingRejoinToken = null;
		didRejoin = false;
		if (lobbyId) {
			const cookie = loadCookie(lobbyId);
			if (cookie) {
				pendingRejoinToken = cookie.reconnectToken;
				logger.net(`Found reconnect cookie for lobby ${lobbyId} — will send rejoin on open`);
			}
		}

		const result = new Promise<boolean>(res => { _connectResolve = res; });

		// Wait for tunnel to become reachable (both named tunnels and quick tunnels)
		if (ip.includes('trycloudflare.com') || port === 443) {
			const reachable = await waitForTunnel(ip);
			if (!reachable) {
				status = 'error';
				_connectResolve?.(false);
				_connectResolve = null;
				return result;
			}
		}

		const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
		const isDefaultPort = (protocol === 'wss' && port === 443) || (protocol === 'ws' && port === 80);
		const url = isDefaultPort ? `${protocol}://${ip}` : `${protocol}://${ip}:${port}`;

		logger.net(`Page protocol: ${location.protocol}`);
		logger.net(`Connecting → ${url}  lobby="${lobby}"`);
		logger.net(`navigator.onLine: ${navigator.onLine}`);

		const connectStart = performance.now();
		socket = new WebSocket(url);

		logger.net(`Socket created — readyState: ${socket.readyState} (0=CONNECTING)`);

		socket.onopen = () => {
			const ms = Math.round(performance.now() - connectStart);
			logger.net(`Connected ✓ — ${url} (${ms}ms)`);
			lastError = null;
			status = 'connected';
			reconnectAttempts = 0;

			// Fire the rejoin handshake before anything else.
			if (pendingRejoinToken && lobbyId) {
				logger.net(`Sending rejoin for lobbyId=${lobbyId}`);
				socket?.send(JSON.stringify(rejoinMessage(lobbyId, pendingRejoinToken)));
			}

			_connectResolve?.(true);
			_connectResolve = null;
		};

		socket.onmessage = (e) => {
			try {
				const msg = JSON.parse(e.data) as ServerMessage;
				const preview = e.data.length > 200 ? e.data.slice(0, 200) + '...' : e.data;
				logger.net(`Server message (${e.data.length} chars): ${preview}`);

				if (msg.type === 'error') {
					// Rejoin failure (stale cookie / lobby mismatch): drop the cookie and
					// fall back to a fresh join instead of surfacing this as a fatal error.
					if (typeof msg.reason === 'string' && msg.reason.startsWith('rejoin_')) {
						logger.warn(`Rejoin rejected (${msg.reason}) — clearing cookie, continuing as new player`);
						clearCookie();
						pendingRejoinToken = null;
						// Unity already treated this as a normal join (placeholder player),
						// so we just continue — no need to close/reconnect.
						return;
					}

					lastError = msg;
					logger.error(`Server error: ${msg.reason}`);
					intentionalClose = true; // Don't auto-reconnect on server errors
					socket?.close();
				} else if (msg.type === 'player_assigned') {
					playerId = String(msg.playerId);
					playerColor = msg.color;
					if (msg.symbol) playerSymbol = msg.symbol;
					if (msg.name) playerName = msg.name;

					// If this assignment came from a rejoin (we asked and got our symbol/name back), flag it.
					if (pendingRejoinToken && msg.symbol) {
						didRejoin = true;
						logger.net(`Rejoin confirmed — restored symbol=${msg.symbol} name=${msg.name ?? ''}`);
					}
					pendingRejoinToken = null;

					// Persist the (rotated) token so the next reconnect works.
					if (msg.lobbyId && msg.reconnectToken) {
						saveCookie({
							lobbyId: msg.lobbyId,
							reconnectToken: msg.reconnectToken,
							expiresAt: Date.now() + COOKIE_TTL_MS,
						});
					}

					logger.net(`Player assigned id=${playerId} color=${msg.color} symbol=${msg.symbol ?? '—'}`);
				} else if (msg.type === 'symbols_update') {
					availableSymbols = msg.available;
					takenSymbols = msg.taken;
					logger.net(`Symbols update: available=[${msg.available.join(',')}] taken=[${msg.taken.join(',')}]`);
				} else if (msg.type === 'symbol_rejected') {
					lastRejectedSymbol = msg.symbol;
					logger.warn(`Symbol rejected: ${msg.symbol} (${msg.reason})`);
				} else if (msg.type === 'lane_update') {
					lane = msg.lane;
					totalLanes = msg.totalLanes;
					logger.net(`Lane update: lane=${lane}/${totalLanes}`);
				} else if (msg.type === 'score_update') {
					score = msg.score;
					lastHitPoints = msg.lastHitPoints;
					lastRating = msg.rating;
					logger.net(`Score update: ${score} (${msg.rating} +${msg.lastHitPoints})`);
				}
			} catch {
				logger.warn(`Unreadable server message: ${e.data}`);
			}
		};

		socket.onerror = () => {
			// ErrorEvent carries no useful detail cross-origin; log what we can
			const ms = Math.round(performance.now() - connectStart);
			logger.error(`WebSocket error after ${ms}ms — url=${url}`);
			logger.error(`  readyState: ${socket?.readyState} (1=OPEN 2=CLOSING 3=CLOSED)`);
			logger.error(`  Possible causes:`);
			logger.error(`    • Server not reachable (wrong IP/port?)`);
			logger.error(`    • Self-signed cert not trusted (open ${protocol === 'wss' ? `https://${ip}${isDefaultPort ? '' : `:${port}`}` : 'n/a'} in Chrome first)`);
			logger.error(`    • Mixed content blocked (page=https but ws=ws://)`);
			// Don't set status to 'error' here — onclose will fire next and handle reconnect
			_connectResolve?.(false);
			_connectResolve = null;
		};

		socket.onclose = (e) => {
			const ms = Math.round(performance.now() - connectStart);
			logger.net(`Socket closed after ${ms}ms — code=${e.code} wasClean=${e.wasClean} reason="${e.reason || '(none)'}"`);
			// Common close codes
			const hint =
				e.code === 1000 ? 'Normal closure' :
				e.code === 1001 ? 'Endpoint going away' :
				e.code === 1006 ? 'Abnormal closure — likely network/TLS error or server unreachable' :
				e.code === 1015 ? 'TLS handshake failed' :
				'';
			if (hint) logger.warn(`  Close code ${e.code}: ${hint}`);
			socket = null;

			// Auto-reconnect on unexpected closure
			if (!intentionalClose && lobbyInfo && reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
				reconnectAttempts++;
				status = 'connecting';
				logger.net(`Auto-reconnect attempt ${reconnectAttempts}/${MAX_RECONNECT_ATTEMPTS} in ${RECONNECT_DELAY_MS}ms...`);
				reconnectTimer = setTimeout(() => {
					if (lobbyInfo) {
						connect(lobbyInfo.ip, lobbyInfo.port, lobbyInfo.lobby, lobbyInfo.lobbyId, true);
					}
				}, RECONNECT_DELAY_MS);
			} else {
				status = 'disconnected';
			}
		};

		return result;
	}

	function send(msg: ControllerMessage) {
		if (socket?.readyState === WebSocket.OPEN) {
			socket.send(JSON.stringify(msg));
		} else {
			logger.warn(`send() dropped — socket not open (readyState=${socket?.readyState ?? 'null'}) msg=${JSON.stringify(msg)}`);
		}
	}

	function disconnect() {
		intentionalClose = true;
		if (reconnectTimer) { clearTimeout(reconnectTimer); reconnectTimer = null; }
		reconnectAttempts = 0;
		socket?.close();
		lobbyInfo = null;
		lastError = null;
		playerId = null;
		playerColor = null;
		lane = 0;
		totalLanes = 3;
		score = 0;
		lastHitPoints = null;
		lastRating = null;
		availableSymbols = [...ALL_SYMBOLS];
		takenSymbols = [];
		playerSymbol = null;
		playerName = '';
		lastRejectedSymbol = null;
		didRejoin = false;
		pendingRejoinToken = null;
	}

	function bypassForTesting() {
		status = 'connected';
		lobbyInfo = { ip: '0.0.0.0', port: 0, lobby: 'dev' };
	}

	return {
		get status() { return status; },
		get lobbyInfo() { return lobbyInfo; },
		get lastError() { return lastError; },
		get playerId() { return playerId; },
		get playerColor() { return playerColor; },
		get lane() { return lane; },
		get totalLanes() { return totalLanes; },
		get score() { return score; },
		get lastHitPoints() { return lastHitPoints; },
		get lastRating() { return lastRating; },
		get availableSymbols() { return availableSymbols; },
		get takenSymbols() { return takenSymbols; },
		get playerSymbol() { return playerSymbol; },
		get playerName() { return playerName; },
		get lastRejectedSymbol() { return lastRejectedSymbol; },
		get didRejoin() { return didRejoin; },
		setPlayerName(name: string) { playerName = name; },
		clearRejectedSymbol() { lastRejectedSymbol = null; },
		connect: (ip: string, port: number, lobby: string, lobbyId?: string): Promise<boolean> =>
			connect(ip, port, lobby, lobbyId, false),
		send,
		disconnect,
		bypassForTesting
	};
}

export const ws = createWebSocketStore();
