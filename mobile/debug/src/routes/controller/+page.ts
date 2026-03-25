import { redirect } from '@sveltejs/kit';
import { ws } from '$lib/websocket.svelte';

export function load() {
	if (ws.status === 'disconnected' || ws.status === 'error') {
		redirect(307, '/');
	}
}
