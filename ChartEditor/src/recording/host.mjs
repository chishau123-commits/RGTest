import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { randomBytes, randomUUID, timingSafeEqual } from 'node:crypto';
import { WebSocketServer, WebSocket } from 'ws';
import { ClockSync, RecordingSession, PROTOCOL } from './session.mjs';

const mime = { '.html': 'text/html; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8' };
const files = new Map([['/', 'src/tablet/index.html'], ['/tablet.mjs', 'src/tablet/tablet.mjs'], ['/tablet.css', 'src/tablet/tablet.css'],
  ['/core/chart.mjs', 'src/core/chart.mjs'], ['/ui/render.mjs', 'src/ui/render.mjs']]);
export class TabletHost {
  constructor({ root, onEvent = () => {}, persistTake = async () => {} }) {
    this.root = root; this.onEvent = onEvent; this.persistTake = persistTake; this.devices = new Map();
    this.session = new RecordingSession(); this.server = null; this.token = null;
  }
  async start(port = 0, bindAddress = '0.0.0.0') {
    if (this.server) return this.info();
    this.token = randomBytes(24).toString('base64url');
    this.server = http.createServer(async (req, res) => {
      try {
        const url = new URL(req.url, 'http://local');
        const relative = files.get(url.pathname);
        if (req.method !== 'GET' || !relative) { res.writeHead(404); res.end(); return; }
        const socketOrigin = `ws://${new URL(`http://${req.headers.host}`).host}`;
        res.setHeader('Content-Security-Policy', `default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self' ${socketOrigin}; img-src 'self' data:; frame-ancestors 'none'; object-src 'none'; base-uri 'none'`);
        res.setHeader('Cache-Control', 'no-store'); res.setHeader('X-Content-Type-Options', 'nosniff');
        res.setHeader('Referrer-Policy', 'no-referrer');
        res.writeHead(200, { 'Content-Type': mime[path.extname(relative)] });
        res.end(await fs.readFile(path.join(this.root, relative)));
      } catch { res.writeHead(500); res.end('Unable to read client'); }
    });
    this.wss = new WebSocketServer({ noServer: true, maxPayload: 64 * 1024 });
    this.server.on('upgrade', (req, socket, head) => {
      const url = new URL(req.url, 'http://local');
      const supplied = Buffer.from(url.searchParams.get('token') || ''), expected = Buffer.from(this.token);
      const origin = req.headers.origin;
      if (url.pathname !== '/touch' || supplied.length !== expected.length || !timingSafeEqual(supplied, expected) ||
        !origin || origin !== `http://${req.headers.host}` || this.devices.size >= 4) { socket.destroy(); return; }
      this.wss.handleUpgrade(req, socket, head, ws => this.connect(ws));
    });
    await new Promise((resolve, reject) => {
      this.server.once('error', reject);
      this.server.listen(port, bindAddress, resolve);
    });
    this.timer = setInterval(() => this.pingAll(), 2000); this.timer.unref();
    return this.info();
  }
  info() {
    const port = this.server?.address()?.port;
    const addresses = Object.values(os.networkInterfaces()).flat().filter(x => x?.family === 'IPv4' && !x.internal).map(x => x.address);
    return { running: !!port, port, urls: port ? [...new Set([...addresses, '127.0.0.1'])].map(ip => `http://${ip}:${port}/#${this.token}`) : [],
      devices: [...this.devices.values()].map(d => ({ id: d.id, name: d.name, connected: true, model: d.clock.model, peakTouches: d.peakTouches })) };
  }
  connect(ws) {
    const d = { id: randomUUID(), name: '平板', ws, clock: new ClockSync(), pendingPings: new Map(), pingSeq: 0, peakTouches: 0, hello: false,
      windowStarted: this.session.nowUs(), messages: 0, queue: Promise.resolve(), models: new Map() };
    this.devices.set(d.id, d);
    const processMessage = async bytes => {
      try {
        const now = this.session.nowUs();
        if (now - d.windowStarted > 1e6) { d.windowStarted = now; d.messages = 0; }
        if (++d.messages > 512) throw new Error('E_RATE: 发送过快');
        const m = JSON.parse(bytes.toString());
        if (!m || typeof m !== 'object') throw new Error('E_MESSAGE');
        if (m.type === 'Hello') {
          if (m.protocolVersion !== PROTOCOL) throw new Error('E_VERSION');
          if (d.hello) throw new Error('E_HELLO');
          d.hello = true; d.name = String(m.deviceName || '平板').slice(0, 80);
          this.send(d, { type: 'Pair', protocolVersion: PROTOCOL, deviceId: d.id, sessionId: this.session.sessionId });
          this.send(d, this.session.snapshot());
          for (let i = 0; i < 6; i++) setTimeout(() => { if (this.devices.has(d.id)) this.ping(d); }, i * 120);
        } else if (!d.hello) throw new Error('E_HELLO');
        else if (m.type === 'ClockPong') {
          const pcSendUs = d.pendingPings.get(m.seq);
          if (pcSendUs === undefined) throw new Error('E_CLOCK_SEQUENCE');
          d.pendingPings.delete(m.seq);
          const model = d.clock.add({ pcSendUs, clientReceiveUs: m.clientReceiveUs, clientSendUs: m.clientSendUs, pcReceiveUs: this.session.nowUs() });
          d.models.set(model.id, model);
          while (d.models.size > 64) d.models.delete(d.models.keys().next().value);
          this.send(d, { type: 'ClockModel', model }); this.onEvent({ type: 'devices', info: this.info() });
        } else if (m.type === 'TouchBatch') {
          const ack = this.session.acceptBatch(m, d);
          // Persist retries too: a preceding disk failure must not produce a premature ACK.
          const raw = this.session.findTake(m.takeId);
          await this.persistTake(raw);
          if (ack.accepted) {
            d.peakTouches = Math.max(d.peakTouches, Math.min(64, Number(m.activeTouches) || 1));
            if (ack.historical) this.onEvent({ type: 'take-updated', take: this.session.serializable(raw) });
            else this.onEvent({ type: 'samples', count: this.session.take?.samples.length || 0 });
          }
          this.send(d, ack);
        } else throw new Error('E_MESSAGE: 未知消息');
      } catch (e) { this.send(d, { type: 'Error', message: String(e.message).slice(0, 200) }); }
    };
    ws.on('message', bytes => { d.queue = d.queue.then(() => processMessage(bytes)); });
    ws.on('error', () => {});
    ws.on('close', async () => {
      await d.queue;
      this.devices.delete(d.id);
      const take = this.session.abort('平板断线：结束本轮，重连需要开始新的 Take');
      if (take) { await this.persistTake(take).catch(() => {}); this.onEvent({ type: 'take-ended', take }); }
      this.session.generation++;
      this.broadcast(); this.onEvent({ type: 'devices', info: this.info() });
    });
    this.onEvent({ type: 'devices', info: this.info() });
  }
  send(d, message) { if (d.ws.readyState === WebSocket.OPEN) d.ws.send(JSON.stringify(message)); }
  ping(d) {
    if (!d.hello) return;
    const seq = d.pingSeq++, pcSendUs = this.session.nowUs();
    if (d.pendingPings.size > 16) d.pendingPings.clear();
    d.pendingPings.set(seq, pcSendUs); this.send(d, { type: 'ClockPing', seq, pcSendUs });
  }
  pingAll() { for (const d of this.devices.values()) this.ping(d); }
  broadcast() { for (const d of this.devices.values()) this.send(d, this.session.snapshot()); }
  async command(m) {
    await Promise.all([...this.devices.values()].map(d => d.queue));
    let ended;
    if (m.type === 'scene') { ended = this.session.abort('编辑谱面'); this.session.setScene(m.chart, { chartHash: m.chartHash, media: m.media }); }
    else if (m.type === 'transport') { if (m.discontinuity) ended = this.session.abort('Seek / 暂停 / 恢复'); this.session.setTransport(m.anchor, m.discontinuity); }
    else if (m.type === 'record-begin') {
      if (![...this.devices.values()].some(d => d.hello && d.clock.model?.sampleCount >= 3 && this.session.nowUs() - d.clock.model.measuredPcUs < 15e6))
        throw new Error('请先连接平板并等待时间同步');
      if (this.session.take) throw new Error('已经在录制');
      this.session.begin();
    } else if (m.type === 'record-end') ended = this.session.end();
    else throw new Error('Unknown host command');
    if (ended) { await this.persistTake(ended); this.onEvent({ type: 'take-ended', take: ended }); }
    this.broadcast(); return { snapshot: this.session.snapshot(), take: ended || null };
  }
  async stop() {
    if (!this.server) return;
    clearInterval(this.timer);
    await Promise.all([...this.devices.values()].map(d => d.queue));
    const take = this.session.end('关闭连接');
    if (take) { await this.persistTake(take); this.onEvent({ type: 'take-ended', take }); }
    for (const d of this.devices.values()) d.ws.terminate(); this.devices.clear();
    await new Promise(resolve => this.wss.close(resolve));
    await new Promise(resolve => this.server.close(resolve));
    this.server = null; this.token = null;
  }
}
