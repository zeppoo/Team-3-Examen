<script lang="ts">
	import { logger } from './logger.svelte';

	let open = $state(false);
	let logEl = $state<HTMLDivElement | null>(null);

	const colors: Record<string, string> = {
		info:  '#d1d5db',
		warn:  '#fbbf24',
		error: '#f87171',
		net:   '#60a5fa',
	};

	$effect(() => {
		// scroll to bottom when new entries arrive
		logger.entries.length;
		if (open && logEl) logEl.scrollTop = logEl.scrollHeight;
	});
</script>

<!-- Toggle button — always visible -->
<button
	class="debug-toggle"
	onclick={() => open = !open}
	aria-label="Toggle debug panel"
>
	{open ? '✕' : '⚙'}
</button>

{#if open}
<div class="debug-panel">
	<div class="debug-header">
		<span>Debug Log</span>
		<button onclick={() => logger.clear()}>clear</button>
	</div>
	<div class="debug-log" bind:this={logEl}>
		{#each logger.entries as entry (entry.id)}
			<div class="entry">
				<span class="time">{entry.time}</span>
				<span class="badge" style="color:{colors[entry.level]}">[{entry.level}]</span>
				<span class="msg" style="color:{colors[entry.level]}">{entry.msg}</span>
			</div>
		{/each}
		{#if logger.entries.length === 0}
			<div class="empty">No logs yet.</div>
		{/if}
	</div>
</div>
{/if}

<style>
	.debug-toggle {
		position: fixed;
		bottom: 16px;
		right: 16px;
		z-index: 9999;
		width: 40px;
		height: 40px;
		border-radius: 50%;
		background: #1f2937;
		border: 1px solid #374151;
		color: #9ca3af;
		font-size: 1.1rem;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.debug-panel {
		position: fixed;
		bottom: 64px;
		right: 12px;
		z-index: 9998;
		width: min(420px, 95vw);
		max-height: 50vh;
		background: #0f172a;
		border: 1px solid #1e293b;
		border-radius: 12px;
		display: flex;
		flex-direction: column;
		font-family: monospace;
		font-size: 0.7rem;
		overflow: hidden;
		box-shadow: 0 8px 32px rgba(0,0,0,0.6);
	}

	.debug-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 6px 12px;
		background: #1e293b;
		color: #94a3b8;
		font-size: 0.75rem;
		font-weight: 600;
	}

	.debug-header button {
		background: none;
		border: none;
		color: #64748b;
		cursor: pointer;
		font-size: 0.7rem;
	}

	.debug-log {
		overflow-y: auto;
		padding: 6px 8px;
		display: flex;
		flex-direction: column;
		gap: 2px;
	}

	.entry {
		display: flex;
		gap: 6px;
		line-height: 1.4;
		word-break: break-all;
	}

	.time   { color: #475569; flex-shrink: 0; }
	.badge  { flex-shrink: 0; }
	.msg    { flex: 1; }

	.empty { color: #334155; padding: 8px; }
</style>
