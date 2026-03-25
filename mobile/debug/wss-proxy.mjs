/**
 * WSS → WS proxy
 *
 * Accepts secure WebSocket connections (wss://) from the phone and forwards
 * them to Unity's plain WebSocket server (ws://localhost) on the same machine.
 *
 * Run once before launching Unity:
 *   node wss-proxy.mjs
 *
 * The QR code will point to wss://<ip>:8443 automatically when this is running.
 *
 * Requires the self-signed cert in certs/ to be trusted on the phone.
 * Install certs/rootCA.crt on the Android device once:
 *   Settings → Security → Install certificate → CA certificate
 */

import https from 'https';
import fs from 'fs';
import path from 'path';
import { WebSocketServer, WebSocket } from 'ws';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const WSS_PORT    = 8443;  // phone connects here (wss://)
const UNITY_PORT  = 8080;  // Unity WebSocket server (ws://localhost)
const UNITY_HOST  = '127.0.0.1';

const server = https.createServer({
    cert: fs.readFileSync(path.join(__dirname, 'certs/cert.pem')),
    key:  fs.readFileSync(path.join(__dirname, 'certs/key.pem')),
});

const wss = new WebSocketServer({ server });

wss.on('connection', (client) => {
    const unity = new WebSocket(`ws://${UNITY_HOST}:${UNITY_PORT}`);

    unity.on('open', () => {
        console.log('[proxy] phone ↔ unity connected');
    });

    // phone → unity
    client.on('message', (data) => {
        if (unity.readyState === WebSocket.OPEN) unity.send(data);
    });

    // unity → phone
    unity.on('message', (data) => {
        if (client.readyState === WebSocket.OPEN) client.send(data);
    });

    const cleanup = (label) => () => {
        console.log(`[proxy] ${label} closed`);
        if (unity.readyState === WebSocket.OPEN)  unity.close();
        if (client.readyState === WebSocket.OPEN) client.close();
    };

    client.on('close', cleanup('phone'));
    unity.on('close',  cleanup('unity'));

    client.on('error', (e) => console.error('[proxy] phone error:', e.message));
    unity.on('error',  (e) => console.error('[proxy] unity error:', e.message));
});

server.listen(WSS_PORT, '0.0.0.0', () => {
    console.log(`[proxy] wss://0.0.0.0:${WSS_PORT} → ws://${UNITY_HOST}:${UNITY_PORT}`);
});
