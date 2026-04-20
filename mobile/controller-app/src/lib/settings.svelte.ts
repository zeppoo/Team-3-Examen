const STORAGE_KEY = 'controller-settings';

interface Settings {
	volume: number;      // 0–100
	rumble: number;      // 0–100
	flipped: boolean;    // true = scratchpad rechts, slider links
}

const defaults: Settings = {
	volume: 80,
	rumble: 50,
	flipped: false,
};

function load(): Settings {
	try {
		const raw = localStorage.getItem(STORAGE_KEY);
		if (raw) return { ...defaults, ...JSON.parse(raw) };
	} catch { /* ignore */ }
	return { ...defaults };
}

function createSettingsStore() {
	let volume = $state(defaults.volume);
	let rumble = $state(defaults.rumble);
	let flipped = $state(defaults.flipped);

	// Snapshot for cancel support
	let snapshot: Settings | null = null;

	// Load from localStorage on init
	const saved = load();
	volume = saved.volume;
	rumble = saved.rumble;
	flipped = saved.flipped;

	function persist() {
		localStorage.setItem(STORAGE_KEY, JSON.stringify({ volume, rumble, flipped }));
	}

	return {
		get volume() { return volume; },
		set volume(v: number) { volume = Math.max(0, Math.min(100, v)); },

		get rumble() { return rumble; },
		set rumble(v: number) { rumble = Math.max(0, Math.min(100, v)); },

		get flipped() { return flipped; },
		set flipped(v: boolean) { flipped = v; },

		/** Take a snapshot of current values before editing */
		beginEdit() {
			snapshot = { volume, rumble, flipped };
		},

		/** Save current values to localStorage */
		save() {
			persist();
			snapshot = null;
		},

		/** Restore values from snapshot (cancel) */
		cancel() {
			if (snapshot) {
				volume = snapshot.volume;
				rumble = snapshot.rumble;
				flipped = snapshot.flipped;
				snapshot = null;
			}
		},
	};
}

export const settings = createSettingsStore();
