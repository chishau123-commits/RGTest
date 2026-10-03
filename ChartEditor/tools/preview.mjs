// Development-only preview. Files are limited to the ignored .test-output directory.
// No desktop file dialogs or arbitrary filesystem API are exposed by this server.
import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { TabletHost } from '../src/recording/host.mjs';
import { atomicWrite } from '../src/core/storage.mjs';
import QRCode from 'qrcode';
const root = path.resolve(import.meta.dirname, '..'), output = path.join(root, '.test-output'), events = [];
await fs.mkdir(output, { recursive: true });
const host = new TabletHost({ root, onEvent: e => events.push(e), persistTake: t => atomicWrite(path.join(output, `${t.id}.take.json`), JSON.stringify(t)) });
const projectFile = path.join(output, 'saved.ringproject'), recoveryFile = path.join(output, 'recovery.ringproject');
const methods = {
  async open() { return { project: JSON.parse(await fs.readFile(projectFile, 'utf8')), path: projectFile }; },
  async save(p) { await atomicWrite(projectFile, JSON.stringify(p)); return projectFile; },
  async exportChart(text) { const file = path.join(output, 'prototype-chart.json'); await atomicWrite(file, text); return file; },
  async importAudio() { return { name: 'sample-audio.wav', base64: (await fs.readFile(path.join(root, 'assets/sample-audio.wav'))).toString('base64') }; },
  async recover() { return { project: await fs.readFile(recoveryFile, 'utf8').then(JSON.parse).catch(() => null), takes: [] }; },
  async autosave(p) { await atomicWrite(recoveryFile, JSON.stringify(p)); return true; },
  clock() { return performance.now() * 1000; },
  async tabletStart() { const info = await host.start(); return { ...info, qr: await QRCode.toDataURL(info.urls[0], { width: 170, margin: 1 }) }; },
  async tabletStop() { await host.stop(); return host.info(); },
  tabletCommand(m) { return host.command(m); },
  async exportTake(t) { const file = path.join(output, 'exported.take.json'); await atomicWrite(file, JSON.stringify(t)); return file; },
  events() { return events.splice(0); }
};
const mime = { '.html': 'text/html; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.wav': 'audio/wav' };
const server = http.createServer(async (req, res) => {
  try {
    const origin = `http://${req.headers.host}`;
    if (!/^127\.0\.0\.1:\d+$/.test(req.headers.host || '')) { res.writeHead(403); res.end(); return; }
    if (req.url === '/preview-rpc' && req.method === 'POST') {
      if (req.headers.origin !== origin || req.headers['x-ring-preview'] !== '1') { res.writeHead(403); res.end(); return; }
      let body = ''; for await (const chunk of req) { body += chunk; if (body.length > 200 * 1024 * 1024) throw new Error('Request too large'); }
      const { method, args } = JSON.parse(body);
      if (!Object.hasOwn(methods, method)) throw new Error('Unknown preview method');
      const value = await methods[method](...args); res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ value })); return;
    }
    const url = new URL(req.url, origin), relative = url.pathname === '/' ? 'src/ui/index.html' : decodeURIComponent(url.pathname).slice(1);
    const file = path.resolve(root, relative), bounded = path.relative(root, file);
    if (bounded.startsWith('..') || path.isAbsolute(bounded) || !/^(src[\\/](ui|core)[\\/]|assets[\\/])/.test(bounded) || req.method !== 'GET') { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', mime[path.extname(file)] || 'text/plain'); res.setHeader('Cache-Control', 'no-store');
    res.end(await fs.readFile(file));
  } catch (e) { res.writeHead(400, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ error: e.message })); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
console.log(`http://127.0.0.1:${server.address().port}/src/ui/index.html`);
process.on('SIGINT', async () => { await host.stop(); server.close(); });
