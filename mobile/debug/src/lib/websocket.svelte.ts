import { logger } from './logger.svelte';

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

function createWebSocketStore() {
	let socket: WebSocket | null = null;

	let status = $state<ConnectionStatus>('disconnected');
	let lobbyInfo = $state<{ ip: string; port: number; lobby: string } | null>(null);

	function connect(ip: string, port: number, lobby: string) {
		if (socket) socket.close();

		status = 'connecting';
		lobbyInfo = { ip, port, lobby };

		const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
		const url = `${protocol}://${ip}:${port}`;
		logger.net(`Connecting to ${url} (lobby: ${lobby})`);
		socket = new WebSocket(url);

		socket.onopen = () => {
			logger.net(`Connected to ${url}`);
			status = 'connected';
		};

		socket.onerror = (e) => {
			logger.net(`WebSocket error on ${url}: ${JSON.stringify(e)}`);
			status = 'error';
		};

		socket.onclose = (e) => {
			logger.net(`WebSocket closed — code=${e.code} reason="${e.reason}" wasClean=${e.wasClean}`);
			status = 'disconnected';
			socket = null;
		};
	}

	function send(button: string, state: 'press' | 'release') {
		if (socket?.readyState === WebSocket.OPEN) {
			socket.send(JSON.stringify({ button, state }));
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
