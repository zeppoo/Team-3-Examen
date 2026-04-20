<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';
	import type { LobbyInfo } from '$lib/messages';
	import jsQR from 'jsqr';
	import OrientationGuard from '$lib/OrientationGuard.svelte';

	let videoEl = $state<HTMLVideoElement | null>(null);
	let canvasEl = $state<HTMLCanvasElement | null>(null);
	let error = $state<string | null>(null);
	let scanning = $state(false);
	let connecting = $state(false);
	let stream: MediaStream | null = null;
	let animFrame: number;

	const hasBarcodeDetector = typeof globalThis !== 'undefined' && 'BarcodeDetector' in globalThis;

	async function startScanner() {
		error = null;
		scanning = true;

		try {
			stream = await navigator.mediaDevices.getUserMedia({
				video: { facingMode: 'environment' }
			});
		} catch {
			error = 'Camera access denied.';
			scanning = false;
			return;
		}

		if (videoEl) {
			videoEl.srcObject = stream;
			await videoEl.play();
		}

		if (hasBarcodeDetector) {
			// @ts-expect-error BarcodeDetector not yet in TS lib
			const detector = new BarcodeDetector({ formats: ['qr_code'] });

			async function detect() {
				if (!videoEl || videoEl.readyState < 2) {
					animFrame = requestAnimationFrame(detect);
					return;
				}

				try {
					const codes = await detector.detect(videoEl);
					if (codes.length > 0) {
						stopScanner();
						handleQR(codes[0].rawValue);
						return;
					}
				} catch {
					// frame not ready yet, keep scanning
				}

				animFrame = requestAnimationFrame(detect);
			}

			animFrame = requestAnimationFrame(detect);
		} else {
			// Fallback: use jsQR for iOS and browsers without BarcodeDetector
			function detectJsQR() {
				if (!videoEl || videoEl.readyState < 2 || !canvasEl) {
					animFrame = requestAnimationFrame(detectJsQR);
					return;
				}

				const ctx = canvasEl.getContext('2d', { willReadFrequently: true });
				if (!ctx) {
					animFrame = requestAnimationFrame(detectJsQR);
					return;
				}

				canvasEl.width = videoEl.videoWidth;
				canvasEl.height = videoEl.videoHeight;
				ctx.drawImage(videoEl, 0, 0, canvasEl.width, canvasEl.height);
				const imageData = ctx.getImageData(0, 0, canvasEl.width, canvasEl.height);
				const code = jsQR(imageData.data, imageData.width, imageData.height);

				if (code) {
					stopScanner();
					handleQR(code.data);
					return;
				}

				animFrame = requestAnimationFrame(detectJsQR);
			}

			animFrame = requestAnimationFrame(detectJsQR);
		}
	}

	function stopScanner() {
		cancelAnimationFrame(animFrame);
		stream?.getTracks().forEach((t) => t.stop());
		stream = null;
		scanning = false;
	}

	function devBypass() {
		ws.bypassForTesting();
		goto('/select');
	}

	async function scanFromImage(e: Event) {
		error = null;
		const file = (e.target as HTMLInputElement).files?.[0];
		if (!file) return;

		const img = new Image();
		const objectUrl = URL.createObjectURL(file);
		img.src = objectUrl;
		await new Promise((res) => (img.onload = res));

		if (hasBarcodeDetector) {
			// @ts-expect-error BarcodeDetector not yet in TS lib
			const detector = new BarcodeDetector({ formats: ['qr_code'] });

			let codes: { rawValue: string }[] = [];
			try {
				codes = await detector.detect(img);
			} catch (e) {
				error = `Detector threw: ${e}`;
				URL.revokeObjectURL(objectUrl);
				return;
			}
			URL.revokeObjectURL(objectUrl);

			if (codes.length === 0) {
				error = `No QR found in image (${img.width}×${img.height}px). Try a clearer/larger image.`;
				return;
			}

			handleQR(codes[0].rawValue);
		} else {
			// Fallback: use jsQR for image scanning
			const canvas = document.createElement('canvas');
			canvas.width = img.width;
			canvas.height = img.height;
			const ctx = canvas.getContext('2d');
			if (!ctx) {
				error = 'Could not create canvas context.';
				URL.revokeObjectURL(objectUrl);
				return;
			}
			ctx.drawImage(img, 0, 0);
			URL.revokeObjectURL(objectUrl);

			const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
			const code = jsQR(imageData.data, imageData.width, imageData.height);

			if (!code) {
				error = `No QR found in image (${img.width}×${img.height}px). Try a clearer/larger image.`;
				return;
			}

			handleQR(code.data);
		}
	}

	async function handleQR(raw: string) {
		const cleaned = raw.replace(/[^\x20-\x7E]/g, '');

		let data: LobbyInfo;
		try {
			data = JSON.parse(cleaned);
		} catch (e) {
			error = `JSON.parse failed: ${e}. Input: "${cleaned}"`;
			return;
		}

		if (!data.ip) { error = `Missing "ip" in: ${cleaned}`; return; }
		if (!data.port) { error = `Missing "port" in: ${cleaned}`; return; }
		if (!data.lobby) { error = `Missing "lobby" in: ${cleaned}`; return; }

		connecting = true;
		error = null;

		try {
			const ok = await ws.connect(data.ip, Number(data.port), data.lobby, data.lobbyId);
			if (ok) {
				goto('/select');
			} else {
				error = 'Could not connect to lobby. Please try again.';
			}
		} catch (e) {
			error = `ws.connect failed: ${e}`;
		} finally {
			connecting = false;
		}
	}
</script>

<div class="landing">
	<h1 class="landing-title">GAME CONTROLLER</h1>

	{#if connecting}
		<div class="connecting">
			<div class="spinner"></div>
			<p class="connecting-text">CONNECTING TO LOBBY...</p>
		</div>
	{:else if !scanning}
		<button class="paper-btn primary" onclick={startScanner}>
			<img class="paper-btn-bg" src="/UI_paper_square.png" alt="" draggable="false" />
			<span class="paper-btn-label">SCAN LOBBY QR</span>
		</button>
	{:else}
		<div class="scanner-frame">
			<!-- svelte-ignore a11y_media_has_caption -->
			<video bind:this={videoEl} class="scanner-video" playsinline></video>
			<canvas bind:this={canvasEl} class="hidden"></canvas>
			<div class="scanner-overlay">
				<div class="scanner-reticle"></div>
			</div>
		</div>
		<button class="paper-btn" onclick={stopScanner}>
			<img class="paper-btn-bg" src="/UI_paper_square.png" alt="" draggable="false" />
			<span class="paper-btn-label">CANCEL</span>
		</button>
	{/if}

	{#if import.meta.env.DEV && !scanning}
		<div class="dev-buttons">
			<button class="paper-btn small" onclick={devBypass}>
				<img class="paper-btn-bg" src="/UI_paper_square.png" alt="" draggable="false" />
				<span class="paper-btn-label">[DEV] SKIP TO CONTROLLER</span>
			</button>
			<label class="paper-btn small">
				<img class="paper-btn-bg" src="/UI_paper_square.png" alt="" draggable="false" />
				<span class="paper-btn-label">[DEV] SCAN QR FROM IMAGE</span>
				<input type="file" accept="image/*" class="hidden" onchange={scanFromImage} />
			</label>
		</div>
	{/if}

	{#if error}
		<p class="error-text">{error}</p>
	{/if}
</div>

<style>
	.landing {
		position: fixed;
		inset: 0;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 24px;
		padding: 24px;
		background-color: #000;
		background-image: url('/T_WallTextures_01.png');
		background-size: 300px;
		background-repeat: repeat;
		color: #111;
		overflow-y: auto;
	}

	.landing-title {
		font-family: 'Bangers', monospace;
		font-size: 2rem;
		letter-spacing: 0.12em;
		color: #fff;
		text-shadow: 2px 2px 0 #000;
		margin: 0;
		text-align: center;
	}

	/* ── Paper-backed button ── */
	.paper-btn {
		position: relative;
		display: inline-flex;
		align-items: center;
		justify-content: center;
		width: 260px;
		aspect-ratio: 2.2 / 1;
		background: transparent;
		border: none;
		padding: 0;
		cursor: pointer;
		user-select: none;
	}

	.paper-btn.small {
		width: 220px;
		aspect-ratio: 3 / 1;
	}

	.paper-btn-bg {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
		pointer-events: none;
	}

	.paper-btn-label {
		position: relative;
		z-index: 1;
		font-family: 'Bangers', monospace;
		font-size: 1.1rem;
		letter-spacing: 0.15em;
		color: #111;
		text-align: center;
		padding: 0 14px;
	}

	.paper-btn.primary .paper-btn-label {
		font-size: 1.35rem;
	}

	.paper-btn.small .paper-btn-label {
		font-size: 0.85rem;
	}

	.paper-btn:active .paper-btn-label {
		transform: translateY(1px);
	}

	.dev-buttons {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 10px;
	}

	/* ── Scanner ── */
	.scanner-frame {
		position: relative;
		width: 100%;
		max-width: 360px;
		overflow: hidden;
		border-radius: 16px;
		border: 3px solid #111;
	}

	.scanner-video {
		width: 100%;
		display: block;
	}

	.scanner-overlay {
		position: absolute;
		inset: 0;
		display: flex;
		align-items: center;
		justify-content: center;
		pointer-events: none;
	}

	.scanner-reticle {
		width: 12rem;
		height: 12rem;
		border-radius: 10px;
		border: 4px solid #fff;
		opacity: 0.85;
	}

	.hidden { display: none; }

	/* ── Connecting + errors ── */
	.connecting {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 14px;
	}

	.connecting-text {
		font-family: 'Bangers', monospace;
		letter-spacing: 0.12em;
		color: #fff;
		font-size: 0.9rem;
		margin: 0;
	}

	.spinner {
		width: 48px;
		height: 48px;
		border: 4px solid rgba(255, 255, 255, 0.25);
		border-top-color: #fff;
		border-radius: 50%;
		animation: spin 0.8s linear infinite;
	}

	.error-text {
		font-family: 'Bangers', monospace;
		font-size: 0.85rem;
		letter-spacing: 0.08em;
		color: #fff;
		background: rgba(153, 27, 27, 0.85);
		border: 2px solid #7f1d1d;
		border-radius: 10px;
		padding: 10px 16px;
		text-align: center;
		max-width: 320px;
		margin: 0;
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}
</style>

<OrientationGuard required="portrait" />
