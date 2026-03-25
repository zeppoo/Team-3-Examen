type LogLevel = 'info' | 'warn' | 'error' | 'net';

export interface LogEntry {
	id: number;
	level: LogLevel;
	time: string;
	msg: string;
}

let _id = 0;
let entries = $state<LogEntry[]>([]);

function timestamp() {
	const d = new Date();
	return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}:${String(d.getSeconds()).padStart(2,'0')}.${String(d.getMilliseconds()).padStart(3,'0')}`;
}

function push(level: LogLevel, msg: string) {
	entries.push({ id: _id++, level, time: timestamp(), msg });
	if (entries.length > 200) entries.splice(0, entries.length - 200);
}

export const logger = {
	get entries() { return entries; },
	info:  (msg: string) => push('info', msg),
	warn:  (msg: string) => push('warn', msg),
	error: (msg: string) => push('error', msg),
	net:   (msg: string) => push('net',  msg),
	clear: () => { entries.splice(0); },
};

// Intercept console
if (typeof window !== 'undefined') {
	const _log   = console.log.bind(console);
	const _warn  = console.warn.bind(console);
	const _error = console.error.bind(console);

	console.log   = (...a) => { _log(...a);   push('info',  a.map(String).join(' ')); };
	console.warn  = (...a) => { _warn(...a);  push('warn',  a.map(String).join(' ')); };
	console.error = (...a) => { _error(...a); push('error', a.map(String).join(' ')); };
}
