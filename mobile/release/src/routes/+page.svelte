<script lang="ts">
	import { goto } from '$app/navigation';
	import { ws } from '$lib/websocket.svelte';

	let videoEl = $state<HTMLVideoElement | null>(null);
	let error = $state<string | null>(null);
	let scanning = $state(false);
	let stream: MediaStream | null = null;
	let animFrame: number;

	async function startScanner() {
		error = null;
		scanning = true;

		if (!('BarcodeDetector' in window)) {
			error = 'QR scanning not supported in this browser. Use Chrome on Android.';
			scanning = false;
			return;
		}

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
	}

	function stopScanner() {
		cancelAnimationFrame(animFrame);
		stream?.getTracks().forEach((t) => t.stop());
		stream = null;
		scanning = false;
	}

	function devBypass() {
		ws.bypassForTesting();
		goto('/controller');
	}

	async function scanFromImage(e: Event) {
		error = null;
		const file = (e.target as HTMLInputElement).files?.[0];
		if (!file) return;

		if (!('BarcodeDetector' in window)) {
			error = 'BarcodeDetector not supported — use Chrome on desktop or Android.';
			return;
		}

		const img = new Image();
		const objectUrl = URL.createObjectURL(file);
		img.src = objectUrl;
		await new Promise((res) => (img.onload = res));

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

		error = `Raw value: "${codes[0].rawValue}"`;
		handleQR(codes[0].rawValue);
	}

	function handleQR(raw: string) {
		const cleaned = raw.replace(/[^\x20-\x7E]/g, '');

		let data: { ip: string; port: number | string; lobby: string };
		try {
			data = JSON.parse(cleaned);
		} catch (e) {
			error = `JSON.parse failed: ${e}. Input: "${cleaned}"`;
			return;
		}

		if (!data.ip) { error = `Missing "ip" in: ${cleaned}`; return; }
		if (!data.port) { error = `Missing "port" in: ${cleaned}`; return; }
		if (!data.lobby) { error = `Missing "lobby" in: ${cleaned}`; return; }

		try {
			ws.connect(data.ip, Number(data.port), data.lobby);
		} catch (e) {
			error = `ws.connect failed: ${e}`;
			return;
		}

		goto('/controller');
	}
</script>

<div class="flex min-h-screen flex-col items-center justify-center gap-6 bg-gray-950 p-6 text-white">
	<h1 class="text-2xl font-bold tracking-tight">Game Controller</h1>

	{#if !scanning}
		<button
			onclick={startScanner}
			class="rounded-2xl bg-indigo-600 px-10 py-5 text-xl font-semibold active:bg-indigo-700"
		>
			Scan Lobby QR
		</button>
	{:else}
		<div class="relative w-full max-w-sm overflow-hidden rounded-2xl">
			<!-- svelte-ignore a11y_media_has_caption -->
			<video bind:this={videoEl} class="w-full" playsinline></video>
			<div class="pointer-events-none absolute inset-0 flex items-center justify-center">
				<div class="h-48 w-48 rounded-lg border-4 border-indigo-400 opacity-80"></div>
			</div>
		</div>
		<button
			onclick={stopScanner}
			class="rounded-2xl bg-gray-700 px-8 py-4 text-lg font-semibold active:bg-gray-600"
		>
			Cancel
		</button>
	{/if}

	{#if import.meta.env.DEV && !scanning}
		<div class="flex flex-col items-center gap-3">
			<button
				onclick={devBypass}
				class="rounded-xl border border-dashed border-gray-600 px-6 py-3 text-sm text-gray-400 active:bg-gray-800"
			>
				[Dev] Skip to controller
			</button>
			<label class="cursor-pointer rounded-xl border border-dashed border-gray-600 px-6 py-3 text-sm text-gray-400 active:bg-gray-800">
				[Dev] Scan QR from image
				<input type="file" accept="image/*" class="hidden" onchange={scanFromImage} />
			</label>
		</div>
	{/if}

	{#if error}
		<p class="rounded-xl bg-red-900/60 px-4 py-3 text-center text-sm text-red-300">{error}</p>
	{/if}
</div>
