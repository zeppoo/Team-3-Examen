<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';
	import { symbolSelectMessage, ALL_SYMBOLS, type SymbolType } from '$lib/messages';
	import OrientationGuard from '$lib/OrientationGuard.svelte';

	let name = $state('');
	let carouselIndex = $state(0);
	let toast = $state<string | null>(null);
	let toastTimer: ReturnType<typeof setTimeout> | null = null;

	$effect(() => {
		if (ws.playerSymbol && ws.playerName) {
			goto('/controller');
		}
	});

	$effect(() => {
		if (ws.lastRejectedSymbol) {
			showToast(`${ws.lastRejectedSymbol} was just taken`);
			ws.clearRejectedSymbol();
		}
	});

	function showToast(msg: string) {
		toast = msg;
		if (toastTimer) clearTimeout(toastTimer);
		toastTimer = setTimeout(() => { toast = null; }, 2200);
	}

	function cycle(dir: 1 | -1) {
		carouselIndex = (carouselIndex + dir + ALL_SYMBOLS.length) % ALL_SYMBOLS.length;
	}

	function isTaken(symbol: SymbolType): boolean {
		return ws.takenSymbols.includes(symbol);
	}

	function pick() {
		const symbol = ALL_SYMBOLS[carouselIndex];
		const trimmed = name.trim();
		if (!trimmed) { showToast('Enter a name first'); return; }
		if (isTaken(symbol)) { showToast(`${symbol} is taken`); return; }

		ws.setPlayerName(trimmed);
		ws.send(symbolSelectMessage(symbol, trimmed, ws.playerId ?? ''));
	}

	const centerSymbol = $derived(ALL_SYMBOLS[carouselIndex]);
	const centerTaken = $derived(isTaken(centerSymbol));
</script>

<div class="root">
	<div class="paper-bg">
		<img src="/UI_paper_square.png" alt="" class="paper" draggable="false" />

		<div class="content">
			<h1 class="title">PICK YOUR VIBE</h1>

			<input
				class="name-input"
				type="text"
				placeholder="YOUR NAME"
				maxlength="12"
				bind:value={name}
			/>

			<div class="carousel">
				<button class="arrow" onclick={() => cycle(-1)} aria-label="Previous">&lt;</button>

				<div class="symbol-row">
					{#each ALL_SYMBOLS as sym, i (sym)}
						{@const offset = i - carouselIndex}
						{@const taken = isTaken(sym)}
						<div
							class="symbol"
							class:center={offset === 0}
							class:taken
							style="transform: translateX({offset * 110}px) scale({offset === 0 ? 1 : 0.65}); opacity: {Math.abs(offset) > 1 ? 0 : (taken ? 0.3 : 1)};"
						>
							<img src="/symbols/{sym}.png" alt={sym} draggable="false" />
							<span class="symbol-label">{sym.toUpperCase()}</span>
						</div>
					{/each}
				</div>

				<button class="arrow" onclick={() => cycle(1)} aria-label="Next">&gt;</button>
			</div>

			<button
				class="pick-btn"
				class:disabled={centerTaken || !name.trim()}
				disabled={centerTaken || !name.trim()}
				onclick={pick}
			>
				{centerTaken ? 'TAKEN' : 'CONFIRM'}
			</button>
		</div>
	</div>

	{#if toast}
		<div class="toast">{toast}</div>
	{/if}
</div>

<OrientationGuard required="portrait" />

<style>
	.root {
		position: fixed;
		inset: 0;
		background-color: #000;
		background-image: url('/T_WallTextures.png');
		background-size: 300px;
		background-repeat: repeat;
		display: flex;
		align-items: center;
		justify-content: center;
		overflow: hidden;
		touch-action: manipulation;
		user-select: none;
	}

	.paper-bg {
		position: relative;
		width: min(90vw, 420px);
		aspect-ratio: 1;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.paper {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		pointer-events: none;
	}

	.content {
		position: relative;
		z-index: 1;
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 18px;
		padding: 18%;
		width: 100%;
		height: 100%;
		justify-content: center;
	}

	.title {
		margin: 0;
		font-family: 'Bangers', monospace;
		font-size: 1.8rem;
		letter-spacing: 0.08em;
		color: #111;
		text-align: center;
	}

	.name-input {
		font-family: 'Bangers', monospace;
		font-size: 1.2rem;
		padding: 10px 14px;
		border: 2px solid #111;
		border-radius: 8px;
		background: rgba(255, 255, 255, 0.8);
		text-align: center;
		letter-spacing: 0.1em;
		width: 70%;
		outline: none;
		text-transform: uppercase;
	}

	.name-input:focus {
		background: #fff;
	}

	.carousel {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 8px;
		width: 100%;
		height: 120px;
		position: relative;
	}

	.arrow {
		font-family: 'Bangers', monospace;
		font-size: 2rem;
		background: transparent;
		border: none;
		color: #111;
		cursor: pointer;
		padding: 4px 10px;
		z-index: 2;
	}

	.symbol-row {
		position: relative;
		flex: 1;
		height: 100%;
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.symbol {
		position: absolute;
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 4px;
		transition: transform 0.25s ease, opacity 0.25s ease;
	}

	.symbol img {
		width: 80px;
		height: 80px;
		object-fit: contain;
	}

	.symbol-label {
		font-family: 'Bangers', monospace;
		font-size: 0.75rem;
		letter-spacing: 0.1em;
		color: #111;
	}

	.symbol.center .symbol-label {
		font-size: 0.9rem;
	}

	.pick-btn {
		font-family: 'Bangers', monospace;
		font-size: 1.3rem;
		letter-spacing: 0.15em;
		padding: 10px 28px;
		border: 3px solid #111;
		border-radius: 10px;
		background: #fff;
		color: #111;
		cursor: pointer;
	}

	.pick-btn.disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}

	.pick-btn:active:not(.disabled) {
		background: #111;
		color: #fff;
	}

	.toast {
		position: absolute;
		bottom: 40px;
		left: 50%;
		transform: translateX(-50%);
		background: rgba(0, 0, 0, 0.85);
		color: #fff;
		font-family: 'Bangers', monospace;
		font-size: 1rem;
		letter-spacing: 0.1em;
		padding: 10px 20px;
		border-radius: 8px;
		z-index: 10;
		animation: toast-in 0.2s ease;
	}

	@keyframes toast-in {
		from { opacity: 0; transform: translate(-50%, 10px); }
		to   { opacity: 1; transform: translate(-50%, 0); }
	}
</style>
