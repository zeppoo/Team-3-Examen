<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';
	import { scratchMessage, sliderMessage } from '$lib/messages';
	import { settings } from '$lib/settings.svelte';
	import { onMount, onDestroy } from 'svelte';

	onMount(() => {
		(screen.orientation as any).lock?.('landscape').catch(() => {});
	});

	onDestroy(() => {
		(screen.orientation as any).unlock?.();
	});

	function withAlpha(hex: string, lighten = 40): string {
		const n = parseInt(hex.replace('#', ''), 16);
		const r = Math.min(255, (n >> 16) + lighten);
		const g = Math.min(255, ((n >> 8) & 0xff) + lighten);
		const b = Math.min(255, (n & 0xff) + lighten);
		return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
	}

	const padColor = $derived(ws.playerColor ?? '#444455');
	const padGlow  = $derived(withAlpha(padColor));

	// ── Settings panel ──
	let settingsOpen = $state(false);

	// ── Scratchpad state ──
	let scratchActive = $state(false);
	let lastY = $state(0);
	let velocity = $state(0);
	let rotation = $state(0);
	let animFrame: number;

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
		ws.send(scratchMessage(velocity, ws.playerId ?? ''));

		const rumbleScale = settings.rumble / 100;
		const intensity = Math.min(Math.round(Math.abs(velocity) * 6 * rumbleScale), 80);
		if (intensity > 2) navigator.vibrate?.(intensity);
	}

	function scratchEnd() {
		scratchActive = false;
		decelerate();
	}

	function decelerate() {
		if (Math.abs(velocity) < 0.2) { velocity = 0; return; }
		velocity *= 0.96;
		rotation += velocity;
		animFrame = requestAnimationFrame(decelerate);
	}

	// ── DJ Slider state ──
	let sliderActive = $state(false);
	let sliderStartY = $state(0);
	const SWIPE_THRESHOLD = 40;
	let sliderOffset = $state(0);
	let sliderZoneEl: HTMLElement | null = $state(null);
	let sliderTrackX = $state(0);
	let sliderTrackY = $state(0);

	function sliderStart(e: PointerEvent) {
		sliderActive = true;
		sliderStartY = e.clientY;
		sliderOffset = 0;
		(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);

		// Move the slider track so it's centered on where the player touched
		if (sliderZoneEl) {
			const rect = sliderZoneEl.getBoundingClientRect();
			sliderTrackX = e.clientX - (rect.left + rect.width / 2);
			sliderTrackY = e.clientY - (rect.top + rect.height / 2);
		}

		const rumbleScale = settings.rumble / 100;
		if (rumbleScale > 0) navigator.vibrate?.(Math.round(15 * rumbleScale));
	}

	function sliderMove(e: PointerEvent) {
		if (!sliderActive) return;
		sliderOffset = e.clientY - sliderStartY;
	}

	function sliderEnd() {
		if (!sliderActive) return;
		sliderActive = false;

		if (Math.abs(sliderOffset) >= SWIPE_THRESHOLD) {
			const direction = sliderOffset < 0 ? 'up' : 'down';
			ws.send(sliderMessage(direction, ws.playerId ?? ''));
			const rumbleScale = settings.rumble / 100;
			if (rumbleScale > 0) navigator.vibrate?.(Math.round(80 * rumbleScale));
		}

		sliderOffset = 0;
	}

	const lanes = $derived(Array.from({ length: ws.totalLanes }, (_, i) => i));

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
		html, body {
			overflow: hidden;
			touch-action: none;
		}
	</style>
</svelte:head>

<div class="root" class:flipped={settings.flipped}>

	<!-- Scratchpad -->
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
			tabindex="0"
		>
			<div class="vinyl-clip">
				<div class="vinyl" style="transform: rotate({rotation}deg); --color: {padColor}; --glow: {padGlow};">
					{#each [0.72, 0.58, 0.44, 0.30] as r}
						<div class="groove" style="width: {r * 100}%; height: {r * 100}%; border-radius: 50%;"></div>
					{/each}
					<div class="vinyl-label" style="--color: {padColor};">
						<span>SCRATCH</span>
					</div>
				</div>
			</div>
		</div>
	</div>

	<!-- Middle: screen -->
	<div class="screen">
		<div class="screen-inner" style="--sc: {padColor}; --sc-glow: {padGlow};">
			<div class="screen-row">
				<span class="screen-label">PLAYER</span>
				<span class="screen-value">{String(ws.playerId ?? '0').padStart(2, '0')}</span>
			</div>
			<div class="screen-divider"></div>
			<div class="screen-row">
				<span class="screen-label">SCORE</span>
				<span class="screen-value">{String(ws.score).padStart(4, '0')}</span>
			</div>
			{#if ws.lastRating}
				<div class="screen-rating {ws.lastRating}">{ws.lastRating.toUpperCase()}</div>
			{/if}
			<div class="screen-divider"></div>
			<div class="lane-indicator">
				<span class="screen-label">LANE</span>
				<div class="lane-dots">
					{#each lanes as i}
						<div
							class="lane-dot"
							class:lane-dot-active={i === ws.lane}
							style="--color: {padColor}; --glow: {padGlow};"
						></div>
					{/each}
				</div>
			</div>
			<button class="settings-btn" style="--sc: {padColor};" onclick={() => { settings.beginEdit(); settingsOpen = true; }}>
				&#9881; SETTINGS
			</button>
		</div>
	</div>

	<!-- DJ Slider -->
	<div
		class="slider-area"
		bind:this={sliderZoneEl}
		onpointerdown={sliderStart}
		onpointermove={sliderMove}
		onpointerup={sliderEnd}
		onpointercancel={sliderEnd}
		role="slider"
		aria-label="Lane switch slider"
		aria-valuenow={ws.lane}
		tabindex="0"
	>
		<div class="slider-track" style="transform: translate({sliderTrackX}px, {sliderTrackY}px); --color: {padColor}; --glow: {padGlow};">
			<div class="slider-label slider-label-top">UP</div>
			<div
				class="slider-knob"
				class:slider-knob-active={sliderActive}
				style="transform: translateY({Math.max(-120, Math.min(120, sliderOffset))}px); --color: {padColor}; --glow: {padGlow};"
			>
				<div class="slider-grip"></div>
				<div class="slider-grip"></div>
				<div class="slider-grip"></div>
			</div>
			<div class="slider-label slider-label-bottom">DOWN</div>
		</div>
	</div>

	<!-- Settings panel (slides down) -->
	<div class="settings-overlay" class:settings-open={settingsOpen}>
		<div class="settings-panel" style="--sc: {padColor}; --sc-glow: {padGlow};">
			<div class="settings-left">
				<div class="settings-header">
					<span class="settings-title">SETTINGS</span>
				</div>

				<div class="settings-faders">
					<!-- Volume fader -->
					<div class="fader-group">
						<span class="fader-value">{settings.volume}</span>
						<div class="fader-track-v">
							<input
								type="range"
								min="0"
								max="100"
								value={settings.volume}
								oninput={(e) => settings.volume = Number((e.target as HTMLInputElement).value)}
								class="fader-input-v"
								style="--sc: {padColor}; --sc-glow: {padGlow};"
							/>
						</div>
						<span class="fader-label">VOLUME</span>
					</div>

					<!-- Rumble fader -->
					<div class="fader-group">
						<span class="fader-value">{settings.rumble}</span>
						<div class="fader-track-v">
							<input
								type="range"
								min="0"
								max="100"
								value={settings.rumble}
								oninput={(e) => settings.rumble = Number((e.target as HTMLInputElement).value)}
								class="fader-input-v"
								style="--sc: {padColor}; --sc-glow: {padGlow};"
							/>
						</div>
						<span class="fader-label">RUMBLE</span>
					</div>

					<!-- Layout toggle -->
					<div class="fader-group">
						<button
							class="toggle-btn"
							class:toggle-active={settings.flipped}
							style="--sc: {padColor};"
							onclick={() => settings.flipped = !settings.flipped}
						>
							{settings.flipped ? 'SLIDER | SCRATCH' : 'SCRATCH | SLIDER'}
						</button>
						<span class="fader-label">LAYOUT</span>
					</div>
				</div>
			</div>

			<div class="settings-actions">
				<button class="action-btn save-btn" style="--sc: {padColor};" onclick={() => { settings.save(); settingsOpen = false; }}>
					SAVE
				</button>
				<button class="action-btn cancel-btn" style="--sc: {padColor};" onclick={() => { settings.cancel(); settingsOpen = false; }}>
					CANCEL
				</button>
			</div>
		</div>
	</div>

	<!-- Error overlay -->
	{#if ws.lastError}
		<div class="error-overlay-err">
			<div class="error-box">
				<span class="error-icon">⚠</span>
				<p class="error-title">
					{'reason' in ws.lastError && ws.lastError.reason === 'lobby_full' ? 'Lobby is full' : 'Connection error'}
				</p>
				<p class="error-reason">{'reason' in ws.lastError ? ws.lastError.reason : 'unknown'}</p>
				<button class="error-back" onclick={disconnect}>Back to scanner</button>
			</div>
		</div>
	{/if}

	<!-- Status bar -->
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

	/* Flipped layout: reverse the order */
	.root.flipped {
		flex-direction: row-reverse;
	}

	/* ── Scratch area ── */
	.scratch-area {
		flex: 1;
		display: flex;
		align-items: center;
		justify-content: flex-start;
		padding: 12px 0 12px 0;
		overflow: hidden;
	}

	.flipped .scratch-area {
		justify-content: flex-end;
	}

	.vinyl-wrapper {
		position: relative;
		height: 100%;
		aspect-ratio: 1;
		transform: translateX(-50%);
		cursor: grab;
	}

	.flipped .vinyl-wrapper {
		transform: translateX(50%);
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

	/* ── Screen (always centered) ── */
	.screen {
		position: absolute;
		left: 50%;
		top: 50%;
		transform: translate(-50%, -50%);
		z-index: 10;
		pointer-events: none;
	}

	.screen-inner {
		pointer-events: all;
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

	.screen-rating {
		font-family: monospace;
		font-size: 0.65rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		text-align: center;
		text-transform: uppercase;
		padding: 2px 0;
		animation: rating-flash 0.6s ease-out;
	}

	.screen-rating.perfect { color: #22c55e; text-shadow: 0 0 8px #22c55e88; }
	.screen-rating.good    { color: #eab308; text-shadow: 0 0 8px #eab30888; }
	.screen-rating.ok      { color: #f97316; text-shadow: 0 0 8px #f9731688; }
	.screen-rating.miss    { color: #ef4444; text-shadow: 0 0 8px #ef444488; }

	@keyframes rating-flash {
		0%   { opacity: 0; transform: scale(1.4); }
		30%  { opacity: 1; transform: scale(1); }
		100% { opacity: 0.7; transform: scale(1); }
	}

	/* ── Lane indicator ── */
	.lane-indicator {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 6px;
	}

	.lane-dots {
		display: flex;
		gap: 6px;
	}

	.lane-dot {
		width: 10px;
		height: 10px;
		border-radius: 50%;
		background: #1a1a2a;
		border: 1px solid #333;
		transition: all 0.2s ease;
	}

	.lane-dot-active {
		background: var(--color);
		border-color: var(--glow);
		box-shadow: 0 0 8px var(--glow), 0 0 16px color-mix(in srgb, var(--glow) 40%, transparent);
	}

	/* ── Settings button ── */
	.settings-btn {
		margin-top: 8px;
		padding: 8px 16px;
		border-radius: 8px;
		background: #1a1a2a;
		border: 1px solid color-mix(in srgb, var(--sc) 30%, transparent);
		color: color-mix(in srgb, var(--sc) 60%, #9ca3af);
		font-family: monospace;
		font-size: 0.65rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		cursor: pointer;
		text-transform: uppercase;
		transition: background 0.15s ease;
	}

	.settings-btn:active {
		background: color-mix(in srgb, var(--sc) 15%, #1a1a2a);
	}

	/* ── DJ Slider ── */
	.slider-area {
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 6px 8px;
		width: 160px;
		touch-action: none;
		cursor: pointer;
	}

	.slider-track {
		position: relative;
		width: 14px;
		height: 100%;
		background: #0a0a10;
		border-radius: 7px;
		border: 1px solid #222;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		box-shadow:
			inset 0 2px 8px rgba(0,0,0,0.8),
			inset 0 0 4px rgba(0,0,0,0.6);
	}

	.slider-track::before {
		content: '';
		position: absolute;
		top: 15%;
		bottom: 15%;
		left: 50%;
		width: 2px;
		transform: translateX(-50%);
		background: repeating-linear-gradient(
			180deg,
			rgba(255,255,255,0.08) 0px,
			rgba(255,255,255,0.08) 1px,
			transparent 1px,
			transparent 8px
		);
		pointer-events: none;
	}

	.slider-label {
		font-family: monospace;
		font-size: 0.4rem;
		font-weight: 700;
		letter-spacing: 0.1em;
		color: rgba(255,255,255,0.15);
		text-transform: uppercase;
		position: absolute;
	}

	.slider-label-top { top: 6px; }
	.slider-label-bottom { bottom: 6px; }

	.slider-knob {
		width: 70px;
		height: 90px;
		border-radius: 6px;
		background:
			linear-gradient(180deg,
				color-mix(in srgb, var(--color) 50%, #444) 0%,
				color-mix(in srgb, var(--color) 40%, #222) 40%,
				color-mix(in srgb, var(--color) 30%, #111) 100%
			);
		border: 1px solid color-mix(in srgb, var(--color) 30%, #555);
		box-shadow:
			inset 0 1px 2px rgba(255,255,255,0.12),
			inset 0 -2px 4px rgba(0,0,0,0.5),
			0 2px 8px rgba(0,0,0,0.6),
			0 0 10px color-mix(in srgb, var(--glow) 15%, transparent);
		pointer-events: none;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 4px;
		transition: box-shadow 0.15s ease, transform 0.05s ease-out;
	}

	.slider-knob-active {
		box-shadow:
			inset 0 1px 2px rgba(255,255,255,0.12),
			inset 0 -2px 4px rgba(0,0,0,0.5),
			0 2px 8px rgba(0,0,0,0.6),
			0 0 18px var(--glow);
	}

	.slider-grip {
		width: 34px;
		height: 2px;
		background: rgba(255,255,255,0.15);
		border-radius: 1px;
	}

	/* ── Settings overlay (slides down) ── */
	.settings-overlay {
		position: absolute;
		inset: 0;
		z-index: 50;
		overflow: hidden;
		pointer-events: none;
	}

	.settings-panel {
		width: 100%;
		height: 100%;
		background: #0a0a0f;
		display: flex;
		flex-direction: row;
		box-shadow: 0 8px 32px rgba(0,0,0,0.8);
		transform: translateY(-100%);
		transition: transform 0.35s cubic-bezier(0.4, 0, 0.2, 1);
		pointer-events: none;
	}

	.settings-open .settings-panel {
		transform: translateY(0);
		pointer-events: all;
	}

	.settings-left {
		flex: 1;
		display: flex;
		flex-direction: column;
		padding: 12px 32px;
		gap: 8px;
	}

	.settings-header {
		display: flex;
		align-items: center;
	}

	.settings-title {
		font-family: monospace;
		font-size: 1rem;
		font-weight: 700;
		letter-spacing: 0.2em;
		color: color-mix(in srgb, var(--sc) 60%, #9ca3af);
		text-transform: uppercase;
	}

	/* ── Faders (vertical) ── */
	.settings-faders {
		flex: 1;
		display: flex;
		flex-direction: row;
		align-items: center;
		justify-content: center;
		gap: 48px;
	}

	.fader-group {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 10px;
	}

	.fader-label {
		font-family: monospace;
		font-size: 0.85rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		color: rgba(255,255,255,0.55);
		text-transform: uppercase;
	}

	.fader-track-v {
		height: 160px;
		width: 50px;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.fader-input-v {
		-webkit-appearance: slider-vertical;
		appearance: slider-vertical;
		writing-mode: vertical-lr;
		direction: rtl;
		width: 14px;
		height: 100%;
		background: #0a0a10;
		border: 1px solid #222;
		border-radius: 7px;
		outline: none;
		box-shadow: inset 0 1px 4px rgba(0,0,0,0.6);
	}

	.fader-input-v::-webkit-slider-thumb {
		-webkit-appearance: none;
		appearance: none;
		width: 44px;
		height: 26px;
		border-radius: 4px;
		background:
			linear-gradient(180deg,
				color-mix(in srgb, var(--sc) 50%, #444) 0%,
				color-mix(in srgb, var(--sc) 40%, #222) 40%,
				color-mix(in srgb, var(--sc) 30%, #111) 100%
			);
		border: 1px solid color-mix(in srgb, var(--sc) 30%, #555);
		box-shadow:
			inset 0 1px 2px rgba(255,255,255,0.12),
			0 2px 6px rgba(0,0,0,0.5);
		cursor: grab;
	}

	.fader-input-v::-moz-range-thumb {
		width: 44px;
		height: 26px;
		border-radius: 4px;
		background:
			linear-gradient(180deg,
				color-mix(in srgb, var(--sc) 50%, #444) 0%,
				color-mix(in srgb, var(--sc) 40%, #222) 40%,
				color-mix(in srgb, var(--sc) 30%, #111) 100%
			);
		border: 1px solid color-mix(in srgb, var(--sc) 30%, #555);
		box-shadow:
			inset 0 1px 2px rgba(255,255,255,0.12),
			0 2px 6px rgba(0,0,0,0.5);
		cursor: grab;
	}

	.fader-value {
		font-family: monospace;
		font-size: 1.1rem;
		font-weight: 700;
		color: color-mix(in srgb, var(--sc-glow) 80%, transparent);
		text-shadow: 0 0 6px color-mix(in srgb, var(--sc) 40%, transparent);
	}

	/* ── Layout toggle ── */
	.toggle-btn {
		padding: 14px 24px;
		border-radius: 8px;
		background: #1a1a2a;
		border: 1px solid #333;
		color: rgba(255,255,255,0.4);
		font-family: monospace;
		font-size: 0.85rem;
		font-weight: 700;
		letter-spacing: 0.1em;
		cursor: pointer;
		transition: all 0.15s ease;
	}

	.toggle-btn.toggle-active {
		background: color-mix(in srgb, var(--sc) 15%, #1a1a2a);
		border-color: color-mix(in srgb, var(--sc) 40%, #333);
		color: color-mix(in srgb, var(--sc) 70%, #fff);
	}

	/* ── Settings action buttons (right side) ── */
	.settings-actions {
		display: flex;
		flex-direction: column;
		justify-content: center;
		gap: 16px;
		padding: 16px 20px;
		border-left: 1px solid #1a1a2a;
	}

	.action-btn {
		padding: 12px 32px;
		border-radius: 8px;
		font-family: monospace;
		font-size: 0.85rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		text-transform: uppercase;
		cursor: pointer;
		transition: all 0.15s ease;
	}

	.cancel-btn {
		background: #1a1a2a;
		border: 1px solid #333;
		color: rgba(255,255,255,0.5);
	}

	.cancel-btn:active {
		background: #252535;
	}

	.save-btn {
		background: color-mix(in srgb, var(--sc) 25%, #1a1a2a);
		border: 1px solid color-mix(in srgb, var(--sc) 50%, #333);
		color: color-mix(in srgb, var(--sc) 80%, #fff);
	}

	.save-btn:active {
		background: color-mix(in srgb, var(--sc) 35%, #1a1a2a);
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
		z-index: 60;
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
	.error-overlay-err {
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
