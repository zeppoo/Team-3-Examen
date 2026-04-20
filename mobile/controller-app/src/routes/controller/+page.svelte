<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';
	import { scratchMessage, sliderMessage } from '$lib/messages';
	import { settings } from '$lib/settings.svelte';
	import OrientationGuard from '$lib/OrientationGuard.svelte';

	function withAlpha(hex: string, lighten = 40): string {
		const n = parseInt(hex.replace('#', ''), 16);
		const r = Math.min(255, (n >> 16) + lighten);
		const g = Math.min(255, ((n >> 8) & 0xff) + lighten);
		const b = Math.min(255, (n & 0xff) + lighten);
		return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
	}

	const padColor = $derived(ws.playerColor ?? '#444455');
	const padGlow  = $derived(withAlpha(padColor));

	// RGB 0-1 for SVG color matrix filter
	const tintR = $derived(parseInt(padColor.slice(1, 3), 16) / 255);
	const tintG = $derived(parseInt(padColor.slice(3, 5), 16) / 255);
	const tintB = $derived(parseInt(padColor.slice(5, 7), 16) / 255);

	// ── Settings panel ──
	let settingsOpen = $state(false);

	// ── Rumble (Vibration API) support ──
	// Safari/iOS never ship this; desktop Firefox returns false; Chrome/Edge Android works.
	const rumbleSupported = $derived(
		typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function'
	);

	function rumble(duration: number) {
		if (!rumbleSupported) return;
		navigator.vibrate!(duration);
	}

	// ── Custom vertical faders (volume / rumble) ──
	function faderDrag(e: PointerEvent, key: 'volume' | 'rumble') {
		const track = e.currentTarget as HTMLElement;
		track.setPointerCapture(e.pointerId);

		function updateFromEvent(ev: PointerEvent) {
			const rect = track.getBoundingClientRect();
			const y = ev.clientY - rect.top;
			const pct = 1 - Math.max(0, Math.min(1, y / rect.height));
			const value = Math.round(pct * 100);
			settings[key] = value;
		}

		updateFromEvent(e);

		function onMove(ev: PointerEvent) { updateFromEvent(ev); }
		function onUp(ev: PointerEvent) {
			track.releasePointerCapture(ev.pointerId);
			track.removeEventListener('pointermove', onMove);
			track.removeEventListener('pointerup', onUp);
			track.removeEventListener('pointercancel', onUp);
		}
		track.addEventListener('pointermove', onMove);
		track.addEventListener('pointerup', onUp);
		track.addEventListener('pointercancel', onUp);
	}

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
		const rawVelocity = dy * 1.5;
		velocity = settings.flipped ? -rawVelocity : rawVelocity;
		rotation += velocity;
		ws.send(scratchMessage(rawVelocity, ws.playerId ?? ''));

		const rumbleScale = settings.rumble / 100;
		const intensity = Math.min(Math.round(Math.abs(velocity) * 6 * rumbleScale), 80);
		if (intensity > 2) rumble(intensity);
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
		if (rumbleScale > 0) rumble(Math.round(15 * rumbleScale));
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
			if (rumbleScale > 0) rumble(Math.round(80 * rumbleScale));
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

	<!-- SVG tint filter: multiplies RGB by tint color, preserves alpha -->
	<svg style="position:absolute;width:0;height:0;">
		<filter id="tint">
			<feColorMatrix type="matrix" values="
				{tintR} 0 0 0 0
				0 {tintG} 0 0 0
				0 0 {tintB} 0 0
				0 0 0 1 0
			"/>
		</filter>
	</svg>

	<!-- Scratchpad -->
	<div
		class="scratch-area"
		onpointerdown={scratchStart}
		onpointermove={scratchMove}
		onpointerup={scratchEnd}
		onpointerleave={scratchEnd}
		role="slider"
		aria-label="Scratch pad"
		aria-valuenow={rotation}
		tabindex="0"
	>
		<div class="vinyl-wrapper">
			<img class="vinyl-paper" src="/UI_paper_Disc.png" alt="" draggable="false" />
			<div class="vinyl-clip">
					<img
						class="vinyl"
						src="/Disk2.png"
						alt="Scratch disk"
						style="transform: rotate({rotation}deg);"
						draggable="false"
					/>
			</div>
		</div>
	</div>

	<!-- Middle: screen -->
	<div class="screen">
		<div class="paper-card" style="--sc: {padColor}; --sc-glow: {padGlow};">
			<img class="paper-card-bg" src="/UI_paper_square.png" alt="" draggable="false" />

			<div class="paper-card-inner">
				{#if ws.playerSymbol}
					<img
						class="player-symbol"
						src="/symbols/{ws.playerSymbol}.png"
						alt={ws.playerSymbol}
						draggable="false"
					/>
				{/if}
				{#if ws.playerName}
					<div class="player-name">{ws.playerName}</div>
				{/if}

				<div class="card-row">
					<span class="card-label">SCORE</span>
					<span class="card-value">{String(ws.score).padStart(4, '0')}</span>
				</div>

				{#if ws.lastRating}
					<div class="card-rating {ws.lastRating}">{ws.lastRating.toUpperCase()}</div>
				{/if}

				<div class="lane-indicator">
					<span class="card-label">LANE</span>
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

				<button class="settings-btn" onclick={() => { settings.beginEdit(); settingsOpen = true; }}>
					&#9881; SETTINGS
				</button>
			</div>
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
		<div class="slider-track" style="transform: translate({sliderTrackX}px, {sliderTrackY}px);">
			<img class="slider-paper" src="/UI_paper_slider.png" alt="" draggable="false" />
			<img class="slider-track-img" src="/UI_main_slider.png" alt="" draggable="false" />
			<img
				class="slider-knob"
				class:slider-knob-active={sliderActive}
				src="/tempo_fader.png"
				alt="Lane slider"
				draggable="false"
				style="transform: translateY({Math.max(-120, Math.min(120, sliderOffset))}px);"
			/>
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
						<div
							class="paper-fader"
							onpointerdown={(e) => faderDrag(e, 'volume')}
							role="slider"
							aria-label="Volume"
							aria-valuenow={settings.volume}
							aria-valuemin={0}
							aria-valuemax={100}
							tabindex="0"
						>
							<img class="paper-fader-paper" src="/UI_paper_slider.png" alt="" draggable="false" />
							<img class="paper-fader-track" src="/UI_main_slider.png" alt="" draggable="false" />
							<img
								class="paper-fader-knob"
								src="/tempo_fader.png"
								alt=""
								draggable="false"
								style="bottom: calc({settings.volume}% - 14px); filter: url(#tint);"
							/>
						</div>
						<span class="fader-label">VOLUME</span>
					</div>

					<!-- Rumble fader -->
					<div class="fader-group" class:fader-disabled={!rumbleSupported}>
						<span class="fader-value">{settings.rumble}</span>
						<div
							class="paper-fader"
							onpointerdown={(e) => faderDrag(e, 'rumble')}
							role="slider"
							aria-label="Rumble"
							aria-valuenow={settings.rumble}
							aria-valuemin={0}
							aria-valuemax={100}
							tabindex="0"
						>
							<img class="paper-fader-paper" src="/UI_paper_slider.png" alt="" draggable="false" />
							<img class="paper-fader-track" src="/UI_main_slider.png" alt="" draggable="false" />
							<img
								class="paper-fader-knob"
								src="/tempo_fader.png"
								alt=""
								draggable="false"
								style="bottom: calc({settings.rumble}% - 14px); filter: url(#tint);"
							/>
						</div>
						<span class="fader-label">RUMBLE</span>
						{#if !rumbleSupported}
							<span class="fader-hint">NOT SUPPORTED ON THIS DEVICE</span>
						{/if}
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

<OrientationGuard required="landscape" />

<style>
	.root {
		position: fixed;
		inset: 0;
		display: flex;
		flex-direction: row;
		background-color: #000;
		background-image: url('/T_WallTextures.png');
		background-size: 300px;
		background-repeat: repeat;
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
		flex: 0 0 35%;
		position: relative;
		display: flex;
		align-items: center;
		padding: 12px 0;
		overflow: visible;
		cursor: grab;
		touch-action: none;
	}

	.scratch-area:active { cursor: grabbing; }

	.vinyl-wrapper {
		position: absolute;
		left: 0;
		top: 50%;
		transform: translate(-50%, -50%);
		height: calc(110% - 24px);
		aspect-ratio: 1;
		pointer-events: none;
		overflow: visible;
	}

	.flipped .vinyl-wrapper {
		left: auto;
		right: 0;
		transform: translate(50%, -50%);
	}

	.vinyl-clip {
		position: absolute;
		top: 50%;
		left: 50%;
		transform: translate(-50%, -50%);
		width: 77%;
		height: 77%;
		overflow: hidden;
	}

	.vinyl-paper {
		position: absolute;
		top: 50%;
		left: 50%;
		transform: translate(-50%, -50%);
		width: 90%;
		height: 90%;
		pointer-events: none;
	}

	.vinyl {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		pointer-events: none;
		filter: url(#tint);
	}

	/* ── Screen (always centered) ── */
	.screen {
		flex: 0 0 30%;
		display: flex;
		align-items: center;
		justify-content: center;
		z-index: 10;
		pointer-events: none;
	}

	.paper-card {
		position: relative;
		width: 90%;
		max-width: 260px;
		aspect-ratio: 1;
		pointer-events: all;
	}

	.paper-card-bg {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		pointer-events: none;
		user-select: none;
	}

	.paper-card-inner {
		position: absolute;
		inset: 0;
		padding: 14%;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 6px;
	}

	.player-symbol {
		width: 54px;
		height: 54px;
		filter: url(#tint);
		pointer-events: none;
	}

	.player-name {
		font-family: 'Bangers', monospace;
		font-size: 1rem;
		letter-spacing: 0.12em;
		color: #111;
		text-align: center;
		line-height: 1;
		margin-top: 2px;
		text-transform: uppercase;
	}

	.card-row {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 0;
		margin-top: 4px;
	}

	.card-label {
		font-family: 'Bangers', monospace;
		font-size: 0.55rem;
		letter-spacing: 0.2em;
		color: rgba(0, 0, 0, 0.55);
		text-transform: uppercase;
	}

	.card-value {
		font-family: 'Bangers', monospace;
		font-size: 1.5rem;
		font-weight: 700;
		color: #111;
		letter-spacing: 0.1em;
		line-height: 1;
	}

	.card-rating {
		font-family: 'Bangers', monospace;
		font-size: 0.7rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		text-align: center;
		text-transform: uppercase;
		animation: rating-flash 0.6s ease-out;
	}

	.card-rating.perfect { color: #15803d; }
	.card-rating.good    { color: #a16207; }
	.card-rating.ok      { color: #c2410c; }
	.card-rating.miss    { color: #b91c1c; }

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
		gap: 4px;
	}

	.lane-dots {
		display: flex;
		gap: 6px;
	}

	.lane-dot {
		width: 10px;
		height: 10px;
		border-radius: 50%;
		background: transparent;
		border: 1.5px solid #111;
		transition: all 0.2s ease;
	}

	.lane-dot-active {
		background: var(--color);
		border-color: #111;
	}

	/* ── Settings button ── */
	.settings-btn {
		margin-top: 6px;
		padding: 6px 14px;
		border-radius: 6px;
		background: transparent;
		border: 1.5px solid #111;
		color: #111;
		font-family: 'Bangers', monospace;
		font-size: 0.65rem;
		font-weight: 700;
		letter-spacing: 0.15em;
		cursor: pointer;
		text-transform: uppercase;
	}

	.settings-btn:active {
		background: #111;
		color: #fff;
	}

	/* ── DJ Slider ── */
	.slider-area {
		flex: 0 0 35%;
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 6px 8px;
		touch-action: none;
		cursor: pointer;
	}

	.slider-track {
		position: relative;
		height: 100%;
		width: 35%;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.slider-paper {
		position: absolute;
		height: 100%;
		width: 100%;
		pointer-events: none;
		z-index: 0;
	}

	.slider-track-img {
		height: 80%;
		width: auto;
		pointer-events: none;
		z-index: 1;
	}

	.slider-knob {
		position: absolute;
		width: 80px;
		height: auto;
		pointer-events: none;
		object-fit: contain;
		z-index: 2;
		filter: url(#tint);
		transition: transform 0.05s ease-out;
	}

	.slider-knob-active {
		filter: url(#tint) drop-shadow(0 2px 8px rgba(0,0,0,0.3));
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
		background-color: #000;
		background-image: url('/T_WallTextures_01.png');
		background-size: 300px;
		background-repeat: repeat;
		display: flex;
		flex-direction: row;
		align-items: center;
		justify-content: center;
		gap: 24px;
		padding: 20px 28px;
		box-shadow: 0 8px 32px rgba(0,0,0,0.5);
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
		gap: 10px;
		max-width: 640px;
	}

	.settings-header {
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.settings-title {
		font-family: 'Bangers', monospace;
		font-size: 1.4rem;
		font-weight: 700;
		letter-spacing: 0.22em;
		color: #fff;
		text-shadow: 2px 2px 0 #000;
		text-transform: uppercase;
	}

	/* ── Faders (vertical) ── */
	.settings-faders {
		flex: 1;
		display: flex;
		flex-direction: row;
		align-items: center;
		justify-content: center;
		gap: 24px;
	}

	.fader-group {
		position: relative;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 8px;
		width: 150px;
		aspect-ratio: 3 / 4;
		padding: 14px 10px;
		background-image: url('/UI_paper_square.png');
		background-size: 100% 100%;
		background-repeat: no-repeat;
	}

	.fader-label {
		font-family: 'Bangers', monospace;
		font-size: 0.95rem;
		font-weight: 700;
		letter-spacing: 0.18em;
		color: #111;
		text-transform: uppercase;
	}

	.fader-hint {
		font-family: 'Bangers', monospace;
		font-size: 0.55rem;
		letter-spacing: 0.12em;
		color: #b91c1c;
		text-align: center;
		max-width: 140px;
		line-height: 1.1;
		margin-top: -2px;
	}

	.fader-disabled .paper-fader,
	.fader-disabled .fader-value {
		opacity: 0.35;
	}

	.fader-disabled .paper-fader {
		pointer-events: none;
	}

	.paper-fader {
		position: relative;
		width: 50px;
		height: 140px;
		display: flex;
		align-items: center;
		justify-content: center;
		touch-action: none;
		cursor: pointer;
	}

	.paper-fader-paper {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		pointer-events: none;
	}

	.paper-fader-track {
		position: relative;
		height: 80%;
		width: auto;
		pointer-events: none;
		z-index: 1;
	}

	.paper-fader-knob {
		position: absolute;
		left: 50%;
		transform: translateX(-50%);
		width: 50px;
		height: auto;
		pointer-events: none;
		z-index: 2;
	}

	.fader-value {
		font-family: 'Bangers', monospace;
		font-size: 1.1rem;
		font-weight: 700;
		color: #111;
	}

	/* ── Layout toggle ── */
	.toggle-btn {
		padding: 10px 16px;
		border-radius: 8px;
		background: transparent;
		border: 2px solid #111;
		color: #111;
		font-family: 'Bangers', monospace;
		font-size: 0.8rem;
		font-weight: 700;
		letter-spacing: 0.1em;
		cursor: pointer;
		text-align: center;
	}

	.toggle-btn.toggle-active {
		background: color-mix(in srgb, var(--sc) 70%, #fff);
		color: #111;
	}

	/* ── Settings action buttons (right side) ── */
	.settings-actions {
		display: flex;
		flex-direction: column;
		justify-content: center;
		gap: 14px;
	}

	.action-btn {
		position: relative;
		width: 160px;
		aspect-ratio: 2.4 / 1;
		background: transparent;
		border: none;
		padding: 0;
		font-family: 'Bangers', monospace;
		font-size: 1.1rem;
		font-weight: 700;
		letter-spacing: 0.2em;
		color: #111;
		text-transform: uppercase;
		cursor: pointer;
		background-image: url('/UI_paper_square.png');
		background-size: 100% 100%;
		background-repeat: no-repeat;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.action-btn:active {
		transform: translateY(1px);
	}

	.save-btn {
		color: color-mix(in srgb, var(--sc) 60%, #111);
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
		color: #555;
		pointer-events: none;
		z-index: 60;
	}

	.dot {
		width: 8px;
		height: 8px;
		border-radius: 50%;
		flex-shrink: 0;
	}

	.lobby { color: #666; }

	.leave {
		pointer-events: all;
		margin-left: 6px;
		padding: 2px 8px;
		border-radius: 6px;
		background: #222;
		border: none;
		color: #aaa;
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
		font-family: 'Bangers', monospace;
	}

	.error-reason {
		margin: 0;
		font-size: 0.75rem;
		color: #6b7280;
		font-family: 'Bangers', monospace;
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
