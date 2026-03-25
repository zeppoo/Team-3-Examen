<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import DebugPanel from '$lib/DebugPanel.svelte';

	const debug = import.meta.env.VITE_DEBUG === 'true';

	let { children } = $props();
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>
{@render children()}
{#if debug}
	<DebugPanel />
{/if}
