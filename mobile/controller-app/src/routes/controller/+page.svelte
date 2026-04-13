<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';
	import { buttonMessage, scratchMessage } from '$lib/messages';
	import { onMount, onDestroy } from 'svelte';

	onMount(() => {
		(screen.orientation as any).lock?.('landscape').catch(() => {});
	});

	onDestroy(() => {
		(screen.orientation as any).unlock?.();
	});

	// Derive a slightly lighter version of the player color for the glow effect.
	function withAlpha(hex: string, lighten = 40): string {
		const n = parseInt(hex.replace('#', ''), 16);
		const r = Math.min(255, (n >> 16) + lighten);
		const g = Math.min(255, ((n >> 8) & 0xff) + lighten);
		const b = Math.min(255, (n & 0xff) + lighten);
		return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
	}

	const padColor = $derived(ws.playerColor ?? '#444455');
	const padGlow  = $derived(withAlpha(padColor));

	const pads = [
		{ id: 'button1', label: '1' },
		{ id: 'button2', label: '2' }
	] as const;

	let pressed = $state<Record<string, boolean>>({});
	const padPointers: Record<string, number> = {};

	// Scratchpad state
	let scratchActive = $state(false);
	let lastY = $state(0);
	let velocity = $state(0);
	let rotation = $state(0);
	let animFrame: number;

	function press(id: 'button1' | 'button2', e: PointerEvent) {
		if (id in padPointers) return; // already held by another pointer
		padPointers[id] = e.pointerId;
		(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
		pressed[id] = true;
		navigator.vibrate?.(5);
		ws.send(buttonMessage(id, 'press'));
	}

	function release(id: 'button1' | 'button2', e: PointerEvent) {
		if (padPointers[id] !== e.pointerId) return; // not the owning pointer
		delete padPointers[id];
		pressed[id] = false;
		ws.send(buttonMessage(id, 'release'));
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
		ws.send(scratchMessage(velocity));

		const intensity = Math.min(Math.round(Math.abs(velocity) * 6), 80);
		if (intensity > 2) navigator.vibrate?.(intensity);
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
			{@const img = pad.id === 'button1' ? ws.button1Image : ws.button2Image}
			<button
				class="pad"
				class:pad-pressed={pressed[pad.id]}
				style="--color: {padColor}; --glow: {padGlow};"
				onpointerdown={(e) => press(pad.id, e)}
				onpointerup={(e) => release(pad.id, e)}
				onpointercancel={(e) => release(pad.id, e)}
			>
				{#if img}
					<img class="pad-symbol" src={img} alt={pad.label} />
				{:else}
					<span class="pad-label">{pad.label}</span>
				{/if}
			</button>
		{/each}
	</div>

	<!-- Middle: digital screen -->
	<div class="screen">
		<div class="screen-inner" style="--sc: {padColor}; --sc-glow: {padGlow};">
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

	<!-- Error overlay -->
	{#if ws.lastError}
		<div class="error-overlay">
			<div class="error-box">
				<span class="error-icon">⚠</span>
				<p class="error-title">
					{ws.lastError.reason === 'lobby_full' ? 'Lobby is full' : 'Connection error'}
				</p>
				<p class="error-reason">{ws.lastError.reason}</p>
				<button class="error-back" onclick={disconnect}>Back to scanner</button>
			</div>
		</div>
	{/if}

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

	.pad-symbol {
		width: 55%;
		height: 55%;
		object-fit: contain;
		pointer-events: none;
		opacity: 0.9;
		filter: drop-shadow(0 0 6px var(--glow));
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
		pointer-events: none;
	}

	/* ── Screen ── */
	.screen {
		flex: 1;
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 20px 8px;
	}

	.screen-inner {
		background: #020c04;
		border: 2px solid color-mix(in srgb, var(--sc) 40%, black 60%);
		border-radius: 12px;
		padding: 16px 20px;
		display: flex;
		flex-direction: column;
		gap: 10px;
		min-width: 110px;
		box-shadow:
			0 0 12px color-mix(in srgb, var(--sc) 15%, transparent),
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
		color: color-mix(in srgb, var(--sc) 50%, transparent);
		text-transform: uppercase;
	}

	.screen-value {
		font-family: monospace;
		font-size: 1.8rem;
		font-weight: 700;
		color: var(--sc-glow);
		text-shadow: 0 0 10px color-mix(in srgb, var(--sc) 80%, transparent);
		letter-spacing: 0.1em;
	}

	.screen-divider {
		height: 1px;
		background: color-mix(in srgb, var(--sc) 15%, transparent);
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

	/* ── Error overlay ── */
	.error-overlay {
		position: absolute;
		inset: 0;
		background: rgba(0, 0, 0, 0.85);
		display: flex;
		align-items: center;
		justify-content: center;
		z-index: 100;
	}

	.error-box {
		background: #1a0a0a;
		border: 1px solid #7f1d1d;
		border-radius: 16px;
		padding: 32px 40px;
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 12px;
		text-align: center;
	}

	.error-icon {
		font-size: 2rem;
		color: #ef4444;
	}

	.error-title {
		margin: 0;
		font-size: 1.1rem;
		font-weight: 700;
		color: #f87171;
		font-family: monospace;
	}

	.error-reason {
		margin: 0;
		font-size: 0.75rem;
		color: #6b7280;
		font-family: monospace;
	}

	.error-back {
		margin-top: 8px;
		padding: 8px 20px;
		border-radius: 8px;
		background: #7f1d1d;
		border: none;
		color: #fca5a5;
		font-size: 0.85rem;
		cursor: pointer;
	}
</style>
