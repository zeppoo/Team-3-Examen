type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

function createWebSocketStore() {
	let socket: WebSocket | null = null;

	let status = $state<ConnectionStatus>('disconnected');
	let lobbyInfo = $state<{ ip: string; port: number; lobby: string } | null>(null);

	function connect(ip: string, port: number, lobby: string) {
		if (socket) socket.close();

		status = 'connecting';
		lobbyInfo = { ip, port, lobby };

		socket = new WebSocket(`ws://${ip}:${port}`);

		socket.onopen = () => {
			status = 'connected';
		};

		socket.onerror = () => {
			status = 'error';
		};

		socket.onclose = () => {
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

	return {
		get status() {
			return status;
		},
		get lobbyInfo() {
			return lobbyInfo;
		},
		connect,
		send,
		disconnect
	};
}

export const ws = createWebSocketStore();
