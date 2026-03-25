<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';

	const pads = [
		{ id: 'pad1', label: '1', color: '#c0392b', glow: '#ff6b6b' },
		{ id: 'pad2', label: '2', color: '#1a6b9a', glow: '#4fc3f7' }
	] as const;

	let pressed = $state<Record<string, boolean>>({});

	// Scratchpad state
	let scratchActive = $state(false);
	let lastY = $state(0);
	let velocity = $state(0);
	let rotation = $state(0);
	let animFrame: number;

	function press(id: string) {
		pressed[id] = true;
		ws.send(id, 'press');
	}

	function release(id: string) {
		pressed[id] = false;
		ws.send(id, 'release');
	}

	function scratchStart(e: PointerEvent) {
		scratchActive = true;
		lastY = e.clientY;
		(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
		cancelAnimationFrame(animFrame);
	}

	function scratchMove(e: PointerEvent) {
		if (!scratchActive) return;
		const dy = e.clientY - lastY;
		lastY = e.clientY;
		velocity = dy * 1.5;
		rotation += velocity;
		ws.send('scratch', 'press');
	}

	function scratchEnd() {
		scratchActive = false;
		decelerate();
	}

	function decelerate() {
		if (Math.abs(velocity) < 0.2) { velocity = 0; return; }
		velocity *= 0.88;
		rotation += velocity;
		animFrame = requestAnimationFrame(decelerate);
	}

	function disconnect() {
		ws.disconnect();
		goto('/');
	}

	const statusColor = $derived(
		ws.status === 'connected' ? '#22c55e'
		: ws.status === 'connecting' ? '#eab308'
		: '#ef4444'
	);
</script>

<svelte:head>
	<style>
		/* Force landscape and prevent scroll/bounce */
		html, body {
			overflow: hidden;
			touch-action: none;
		}
	</style>
</svelte:head>

<!-- Lock landscape via CSS (works when screen orientation API isn't available) -->
<div class="root">

	<!-- Left: 2 drum pads stacked -->
	<div class="pads">
		{#each pads as pad}
			<button
				class="pad"
				class:pad-pressed={pressed[pad.id]}
				style="--color: {pad.color}; --glow: {pad.glow};"
				onpointerdown={() => press(pad.id)}
				onpointerup={() => release(pad.id)}
				onpointerleave={() => release(pad.id)}
			>
				<span class="pad-label">{pad.label}</span>
			</button>
		{/each}
	</div>

	<!-- Middle: digital screen -->
	<div class="screen">
		<div class="screen-inner">
			<div class="screen-row">
				<span class="screen-label">PLAYER</span>
				<span class="screen-value">01</span>
			</div>
			<div class="screen-divider"></div>
			<div class="screen-row">
				<span class="screen-label">SCORE</span>
				<span class="screen-value">0000</span>
			</div>
		</div>
	</div>

	<!-- Right: half DJ scratchpad -->
	<div class="scratch-area">
		<div
			class="vinyl-wrapper"
			onpointerdown={scratchStart}
			onpointermove={scratchMove}
			onpointerup={scratchEnd}
			onpointerleave={scratchEnd}
			role="slider"
			aria-label="Scratch pad"
			aria-valuenow={rotation}
		>
			<!-- Vinyl disc, clipped to left half -->
			<div class="vinyl-clip">
				<div class="vinyl" style="transform: rotate({rotation}deg);">
					<!-- Grooves -->
					{#each [0.72, 0.58, 0.44, 0.30] as r}
						<div class="groove" style="width: {r * 100}%; height: {r * 100}%; border-radius: 50%;"></div>
					{/each}
					<!-- Label in center -->
					<div class="vinyl-label">
						<span>SCRATCH</span>
					</div>
					</div>
			</div>
			</div>
	</div>

	<!-- Status bar overlay (top-left) -->
	<div class="statusbar">
		<div class="dot" style="background: {statusColor};"></div>
		<span>{ws.status}</span>
		{#if ws.lobbyInfo}
			<span class="lobby">· {ws.lobbyInfo.lobby}</span>
		{/if}
		<button class="leave" onclick={disconnect}>Leave</button>
	</div>
</div>

<style>
	.root {
		position: fixed;
		inset: 0;
		display: flex;
		flex-direction: row;
		background: #0a0a0f;
		overflow: hidden;
		touch-action: none;
		user-select: none;
	}

	/* ── Drum pads ── */
	.pads {
		display: flex;
		flex-direction: column;
		gap: 12px;
		padding: 16px;
		width: 38%;
		height: 100%;
		box-sizing: border-box;
	}

	.pad {
		flex: 1;
		border-radius: 18px;
		border: none;
		cursor: pointer;
		position: relative;
		overflow: hidden;
		touch-action: none;

		background:
			radial-gradient(ellipse at 30% 25%, color-mix(in srgb, var(--color) 70%, white 30%) 0%, var(--color) 55%, color-mix(in srgb, var(--color) 60%, black 40%) 100%);

		box-shadow:
			inset 0 3px 6px rgba(255,255,255,0.15),
			inset 0 -4px 8px rgba(0,0,0,0.5),
			0 0 18px color-mix(in srgb, var(--glow) 30%, transparent),
			0 6px 20px rgba(0,0,0,0.6);

		transition: transform 80ms ease, box-shadow 80ms ease;
	}

	.pad::before {
		/* top sheen */
		content: '';
		position: absolute;
		inset: 0;
		border-radius: inherit;
		background: linear-gradient(160deg, rgba(255,255,255,0.18) 0%, transparent 50%);
		pointer-events: none;
	}

	.pad-pressed {
		transform: scale(0.95) translateY(3px);
		box-shadow:
			inset 0 6px 14px rgba(0,0,0,0.6),
			inset 0 -2px 4px rgba(255,255,255,0.05),
			0 0 28px var(--glow),
			0 2px 6px rgba(0,0,0,0.4);
	}

	.pad-label {
		position: absolute;
		bottom: 12px;
		right: 16px;
		font-size: 2rem;
		font-weight: 900;
		color: rgba(255,255,255,0.25);
		font-family: monospace;
		letter-spacing: -1px;
	}

	/* ── Screen ── */
	.screen {
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 20px 8px;
	}

	.screen-inner {
		background: #020c04;
		border: 2px solid #1a3d1a;
		border-radius: 12px;
		padding: 16px 20px;
		display: flex;
		flex-direction: column;
		gap: 10px;
		min-width: 110px;
		box-shadow:
			0 0 12px rgba(0, 255, 80, 0.15),
			inset 0 0 20px rgba(0, 0, 0, 0.8);
		/* Scanline overlay */
		background-image: repeating-linear-gradient(
			0deg,
			transparent,
			transparent 2px,
			rgba(0, 0, 0, 0.15) 2px,
			rgba(0, 0, 0, 0.15) 4px
		);
	}

	.screen-row {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 2px;
	}

	.screen-label {
		font-family: monospace;
		font-size: 0.5rem;
		letter-spacing: 0.2em;
		color: rgba(0, 255, 80, 0.4);
		text-transform: uppercase;
	}

	.screen-value {
		font-family: monospace;
		font-size: 1.8rem;
		font-weight: 700;
		color: #00ff50;
		text-shadow: 0 0 10px rgba(0, 255, 80, 0.8);
		letter-spacing: 0.1em;
	}

	.screen-divider {
		height: 1px;
		background: rgba(0, 255, 80, 0.15);
		margin: 0 4px;
	}

	/* ── Scratch area ── */
	.scratch-area {
		flex: 1;
		display: flex;
		align-items: center;
		justify-content: flex-end;
		padding: 12px 0 12px 0;
		overflow: hidden;
	}

	.vinyl-wrapper {
		position: relative;
		/* Size: full height of container */
		height: 100%;
		aspect-ratio: 1;
		/* Shift disc so only left half is visible, bleeding off right edge */
		transform: translateX(50%);
		cursor: grab;
	}

	.vinyl-wrapper:active { cursor: grabbing; }

	.vinyl-clip {
		position: absolute;
		inset: 0;
		overflow: hidden;
	}

	.vinyl {
		position: absolute;
		inset: 0;
		border-radius: 50%;
		background: radial-gradient(circle at center, #2a2a2a 0%, #111 60%, #050505 100%);
		box-shadow:
			0 0 0 3px #333,
			0 0 40px rgba(0,0,0,0.8);
	}

	.groove {
		position: absolute;
		top: 50%;
		left: 50%;
		transform: translate(-50%, -50%);
		border: 1px solid rgba(255,255,255,0.06);
		pointer-events: none;
	}

	.vinyl-label {
		position: absolute;
		top: 50%;
		left: 50%;
		transform: translate(-50%, -50%);
		width: 28%;
		height: 28%;
		border-radius: 50%;
		background: radial-gradient(circle, #1a1a2e, #0d0d1a);
		border: 2px solid #333;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.vinyl-label span {
		font-size: 0.45rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		color: rgba(255,255,255,0.4);
		text-transform: uppercase;
	}



	/* ── Status bar ── */
	.statusbar {
		position: absolute;
		top: 8px;
		right: 12px;
		display: flex;
		align-items: center;
		gap: 6px;
		font-size: 0.7rem;
		color: #6b7280;
		pointer-events: none;
	}

	.dot {
		width: 8px;
		height: 8px;
		border-radius: 50%;
		flex-shrink: 0;
	}

	.lobby { color: #4b5563; }

	.leave {
		pointer-events: all;
		margin-left: 6px;
		padding: 2px 8px;
		border-radius: 6px;
		background: #1f2937;
		border: none;
		color: #9ca3af;
		font-size: 0.7rem;
		cursor: pointer;
	}
</style>
