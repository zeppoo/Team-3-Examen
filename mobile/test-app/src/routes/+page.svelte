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

	function handleQR(raw: string) {
		try {
			const data = JSON.parse(raw) as { ip: string; port: number; lobby: string };
			if (!data.ip || !data.port || !data.lobby) throw new Error();
			ws.connect(data.ip, data.port, data.lobby);
			goto('/controller');
		} catch {
			error = 'Invalid QR code. Expected {"ip","port","lobby"}.';
		}
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

	{#if error}
		<p class="rounded-xl bg-red-900/60 px-4 py-3 text-center text-sm text-red-300">{error}</p>
	{/if}
</div>
