const { WebSocketServer } = require('ws');
const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.RELAY_PORT || 8080;
const API_KEY = process.env.RELAY_API_KEY || 'shinchan2024';

const clients = new Map();

// WebSocket server for client connections
const wss = new WebSocketServer({ noServer: true });

// HTTP server for API + sender UI
const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, X-Api-Key');

    if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }

    // Health check
    if (req.method === 'GET' && req.url === '/health') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'running', clients: clients.size }));
        return;
    }

    // Sender UI
    if (req.method === 'GET' && (req.url === '/' || req.url === '/index.html')) {
        const html = fs.readFileSync(path.join(__dirname, 'sender.html'), 'utf-8');
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end(html);
        return;
    }

    // Send text
    if (req.method === 'POST' && req.url === '/api/send') {
        if (req.headers['x-api-key'] !== API_KEY) {
            res.writeHead(401, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Invalid API key' }));
            return;
        }
        let body = '';
        req.on('data', c => body += c);
        req.on('end', () => {
            try {
                const msg = JSON.parse(body);
                if (!msg.type || !msg.content) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'type and content required' }));
                    return;
                }
                const payload = JSON.stringify(msg);
                let sent = 0;
                for (const [, c] of clients) {
                    if (c.ws.readyState === 1) { c.ws.send(payload); sent++; }
                }
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ status: 'ok', clients: sent }));
            } catch (e) {
                res.writeHead(400, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: 'Invalid JSON' }));
            }
        });
        return;
    }

    // Send image
    if (req.method === 'POST' && req.url === '/api/send-image') {
        if (req.headers['x-api-key'] !== API_KEY) {
            res.writeHead(401, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Invalid API key' }));
            return;
        }
        const ct = req.headers['content-type'] || '';
        if (!ct.includes('multipart/form-data')) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Expected multipart/form-data' }));
            return;
        }
        const chunks = [];
        req.on('data', c => chunks.push(c));
        req.on('end', () => {
            try {
                const buffer = Buffer.concat(chunks);
                const boundary = ct.split('boundary=')[1];
                const parts = buffer.toString('binary').split('--' + boundary);
                for (const part of parts) {
                    if (part.includes('filename=')) {
                        const start = part.indexOf('\r\n\r\n') + 4;
                        const end = part.lastIndexOf('\r\n');
                        const base64 = Buffer.from(part.slice(start, end), 'binary').toString('base64');
                        const payload = JSON.stringify({ type: 'image', content: base64 });
                        let sent = 0;
                        for (const [, c] of clients) {
                            if (c.ws.readyState === 1) { c.ws.send(payload); sent++; }
                        }
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ status: 'ok', clients: sent }));
                        return;
                    }
                }
                res.writeHead(400, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: 'No file found' }));
            } catch (e) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: e.message }));
            }
        });
        return;
    }

    res.writeHead(404);
    res.end('Not found');
});

// Handle WebSocket upgrade
server.on('upgrade', (req, socket, head) => {
    wss.handleUpgrade(req, socket, head, ws => {
        wss.emit('connection', ws, req);
    });
});

wss.on('connection', (ws, req) => {
    const url = new URL(req.url, 'http://localhost');
    const name = url.searchParams.get('name') || 'unknown';
    const id = Date.now().toString(36) + Math.random().toString(36).slice(2, 6);

    clients.set(id, { ws, name });
    console.log(`[+] ${name} connected (${clients.size} total)`);

    ws.on('close', () => {
        clients.delete(id);
        console.log(`[-] ${name} disconnected (${clients.size} total)`);
    });
    ws.on('error', err => {
        console.error(`[!] ${name} error:`, err.message);
        clients.delete(id);
    });
});

console.log('=========================================');
console.log('  Crayon Shin-chan Relay Server');
console.log(`  Web UI: http://0.0.0.0:${PORT}`);
console.log(`  API:    http://0.0.0.0:${PORT}/api/send`);
console.log(`  WS:     ws://0.0.0.0:${PORT}`);
console.log(`  Key:    ${API_KEY}`);
console.log('=========================================');

server.listen(PORT);
