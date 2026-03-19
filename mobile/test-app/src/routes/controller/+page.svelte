<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';

	const buttons = [
		{ id: 'red', label: 'Red', color: 'bg-red-600 active:bg-red-700' },
		{ id: 'green', label: 'Green', color: 'bg-green-600 active:bg-green-700' },
		{ id: 'blue', label: 'Blue', color: 'bg-blue-600 active:bg-blue-700' }
	] as const;

	function press(id: string) {
		ws.send(id, 'press');
	}

	function release(id: string) {
		ws.send(id, 'release');
	}

	function disconnect() {
		ws.disconnect();
		goto('/');
	}

	const statusColor = $derived(
		ws.status === 'connected'
			? 'bg-green-500'
			: ws.status === 'connecting'
				? 'bg-yellow-500'
				: 'bg-red-500'
	);
</script>

<div class="flex min-h-screen flex-col bg-gray-950 text-white">
	<!-- Header -->
	<div class="flex items-center justify-between px-4 py-3">
		<div class="flex items-center gap-2">
			<div class="h-3 w-3 rounded-full {statusColor}"></div>
			<span class="text-sm text-gray-400 capitalize">{ws.status}</span>
			{#if ws.lobbyInfo}
				<span class="text-sm text-gray-500">· {ws.lobbyInfo.lobby}</span>
			{/if}
		</div>
		<button
			onclick={disconnect}
			class="rounded-lg bg-gray-800 px-3 py-1 text-sm text-gray-300 active:bg-gray-700"
		>
			Leave
		</button>
	</div>

	<!-- Buttons -->
	<div class="flex flex-1 flex-col items-center justify-center gap-6 p-6">
		{#each buttons as btn}
			<button
				class="h-32 w-full max-w-sm rounded-3xl text-3xl font-bold shadow-lg select-none {btn.color}"
				onpointerdown={() => press(btn.id)}
				onpointerup={() => release(btn.id)}
				onpointerleave={() => release(btn.id)}
			>
				{btn.label}
			</button>
		{/each}
	</div>
</div>
