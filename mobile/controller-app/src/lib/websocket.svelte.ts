import { logger } from './logger.svelte';
import type { ControllerMessage, ServerMessage } from './messages';

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

interface WebSocketStore {
	readonly status: ConnectionStatus;
	readonly lobbyInfo: { ip: string; port: number; lobby: string } | null;
	readonly lastError: ServerMessage | null;
	readonly playerColor: string | null;
	readonly button1Image: string | null;
	readonly button2Image: string | null;
	connect(ip: string, port: number, lobby: string): Promise<boolean>;
	send(msg: ControllerMessage): void;
	disconnect(): void;
	bypassForTesting(): void;
}

function createWebSocketStore(): WebSocketStore {
	let socket: WebSocket | null = null;

	let status = $state<ConnectionStatus>('disconnected');
	let lobbyInfo = $state<{ ip: string; port: number; lobby: string } | null>(null);
	let lastError = $state<ServerMessage | null>(null);
	let playerColor = $state<string | null>(null);
	let button1Image = $state<string | null>(null);
	let button2Image = $state<string | null>(null);

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

	async function connect(ip: string, port: number, lobby: string): Promise<boolean> {
		if (socket) {
			logger.net(`Closing existing socket before reconnect`);
			socket.close();
		}

		status = 'connecting';
		lobbyInfo = { ip, port, lobby };
		const result = new Promise<boolean>(res => { _connectResolve = res; });

		if (ip.includes('trycloudflare.com')) {
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
			_connectResolve?.(true);
			_connectResolve = null;
		};

		socket.onmessage = (e) => {
			try {
				const msg = JSON.parse(e.data) as ServerMessage;
				logger.net(`Server message: ${e.data}`);

				if (msg.type === 'error') {
					lastError = msg;
					logger.error(`Server error: ${msg.reason}`);
					socket?.close();
				} else if (msg.type === 'player_assigned') {
					playerColor = msg.color;
					if (msg.button1Image) button1Image = `data:image/png;base64,${msg.button1Image}`;
					if (msg.button2Image) button2Image = `data:image/png;base64,${msg.button2Image}`;
					logger.net(`Player assigned color: ${msg.color}`);
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
			status = 'error';
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
			status = 'disconnected';
			socket = null;
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
		socket?.close();
		lobbyInfo = null;
		lastError = null;
		playerColor = null;
		button1Image = null;
		button2Image = null;
	}

	function bypassForTesting() {
		status = 'connected';
		lobbyInfo = { ip: '0.0.0.0', port: 0, lobby: 'dev' };
	}

	return {
		get status() { return status; },
		get lobbyInfo() { return lobbyInfo; },
		get lastError() { return lastError; },
		get playerColor() { return playerColor; },
		get button1Image() { return button1Image; },
		get button2Image() { return button2Image; },
		connect: (ip: string, port: number, lobby: string): Promise<boolean> => connect(ip, port, lobby),
		send,
		disconnect,
		bypassForTesting
	};
}

export const ws = createWebSocketStore();
