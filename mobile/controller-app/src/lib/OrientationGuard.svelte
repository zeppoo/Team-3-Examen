<script lang="ts">
	import { onMount } from 'svelte';

	type Orientation = 'portrait' | 'landscape';

	let { required }: { required: Orientation } = $props();

	let isWrongOrientation = $state(false);

	function check() {
		if (required === 'landscape') {
			isWrongOrientation = window.innerHeight > window.innerWidth;
		} else {
			isWrongOrientation = window.innerWidth > window.innerHeight;
		}
	}

	onMount(() => {
		try {
			(screen.orientation as any)?.lock?.(required)?.catch?.(() => {});
		} catch {
			// Not supported (iOS Safari, non-fullscreen contexts)
		}

		check();
		window.addEventListener('resize', check);

		return () => {
			window.removeEventListener('resize', check);
			try {
				(screen.orientation as any)?.unlock?.();
			} catch {
				// Not supported
			}
		};
	});
</script>

{#if isWrongOrientation}
	<div class="orientation-overlay">
		<div class="orientation-content">
			<div class="phone-icon" class:rotate-landscape={required === 'landscape'}>
				<div class="phone-body">
					<div class="phone-screen"></div>
				</div>
			</div>
			<p class="orientation-text">
				{required === 'landscape' ? 'Rotate your device to landscape' : 'Rotate your device to portrait'}
			</p>
		</div>
	</div>
{/if}

<style>
	.orientation-overlay {
		position: fixed;
		inset: 0;
		z-index: 9999;
		background: #0a0a0f;
		display: flex;
		align-items: center;
		justify-content: center;
		touch-action: none;
		user-select: none;
	}

	.orientation-content {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 24px;
	}

	.phone-icon {
		animation: rotate-hint 2s ease-in-out infinite;
	}

	.phone-icon.rotate-landscape {
		animation: rotate-to-landscape 2s ease-in-out infinite;
	}

	.phone-body {
		width: 48px;
		height: 80px;
		border: 3px solid rgba(255, 255, 255, 0.5);
		border-radius: 8px;
		display: flex;
		align-items: center;
		justify-content: center;
		position: relative;
	}

	.phone-screen {
		width: 36px;
		height: 60px;
		background: rgba(255, 255, 255, 0.1);
		border-radius: 2px;
	}

	.orientation-text {
		font-family: monospace;
		font-size: 0.9rem;
		font-weight: 600;
		letter-spacing: 0.1em;
		color: rgba(255, 255, 255, 0.5);
		text-align: center;
		text-transform: uppercase;
	}

	@keyframes rotate-to-landscape {
		0%, 30% { transform: rotate(0deg); }
		50%, 80% { transform: rotate(90deg); }
		100% { transform: rotate(0deg); }
	}

	@keyframes rotate-hint {
		0%, 30% { transform: rotate(0deg); }
		50%, 80% { transform: rotate(-90deg); }
		100% { transform: rotate(0deg); }
	}
</style>
