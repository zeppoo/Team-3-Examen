import { logger } from './logger.svelte';
import type { ControllerMessage } from './messages';

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

function createWebSocketStore() {
	let socket: WebSocket | null = null;

	let status = $state<ConnectionStatus>('disconnected');
	let lobbyInfo = $state<{ ip: string; port: number; lobby: string } | null>(null);

	function connect(ip: string, port: number, lobby: string) {
		if (socket) {
			logger.net(`Closing existing socket before reconnect`);
			socket.close();
		}

		status = 'connecting';
		lobbyInfo = { ip, port, lobby };

		const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
		const url = `${protocol}://${ip}:${port}`;

		logger.net(`Page protocol: ${location.protocol}`);
		logger.net(`Connecting → ${url}  lobby="${lobby}"`);
		logger.net(`navigator.onLine: ${navigator.onLine}`);

		const connectStart = performance.now();
		socket = new WebSocket(url);

		logger.net(`Socket created — readyState: ${socket.readyState} (0=CONNECTING)`);

		socket.onopen = () => {
			const ms = Math.round(performance.now() - connectStart);
			logger.net(`Connected ✓ — ${url} (${ms}ms)`);
			status = 'connected';
		};

		socket.onerror = () => {
			// ErrorEvent carries no useful detail cross-origin; log what we can
			const ms = Math.round(performance.now() - connectStart);
			logger.error(`WebSocket error after ${ms}ms — url=${url}`);
			logger.error(`  readyState: ${socket?.readyState} (1=OPEN 2=CLOSING 3=CLOSED)`);
			logger.error(`  Possible causes:`);
			logger.error(`    • Server not reachable (wrong IP/port?)`);
			logger.error(`    • Self-signed cert not trusted (open ${protocol === 'wss' ? `https://${ip}:${port}` : 'n/a'} in Chrome first)`);
			logger.error(`    • Mixed content blocked (page=https but ws=ws://)`);
			status = 'error';
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
	}

	function bypassForTesting() {
		status = 'connected';
		lobbyInfo = { ip: '0.0.0.0', port: 0, lobby: 'dev' };
	}

	return {
		get status() {
			return status;
		},
		get lobbyInfo() {
			return lobbyInfo;
		},
		connect,
		send,
		disconnect,
		bypassForTesting
	};
}

export const ws = createWebSocketStore();
