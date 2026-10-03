import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { once } from 'node:events';
import { WebSocket } from 'ws';
import { TabletHost } from '../src/recording/host.mjs';
import { atomicWrite } from '../src/core/storage.mjs';
import { emptyChart, addNote, identity } from '../src/core/chart.mjs';
const root = fileURLToPath(new URL('..', import.meta.url));
const waitFor = (list, type) => new Promise((resolve, reject) => {
  const deadline = Date.now() + 3000;
  const check = () => { const found = list.find(x => x.type === type); if (found) resolve(found); else if (Date.now() > deadline) reject(new Error(`Timeout waiting for ${type}`)); else setTimeout(check, 10); }; check();
});
test('real HTTP/WebSocket pairing, clock sync, persisted touches, ACK, and disconnect finish', async () => {
  const persisted = [], events = [], host = new TabletHost({ root, persistTake: async t => { persisted.push(t); }, onEvent: e => events.push(e) });
  const info = await host.start(0, '127.0.0.1'), origin = `http://127.0.0.1:${info.port}`;
  const page = await fetch(origin); assert.equal(page.status, 200); assert.match(await page.text(), /RING/);
  assert.ok(page.headers.get('content-security-policy').includes(`ws://127.0.0.1:${info.port}`));
  assert.equal((await fetch(`${origin}/main.cjs`)).status, 404); assert.equal((await fetch(`${origin}/assets/sample-audio.wav`)).status, 404);
  const token = info.urls[0].split('#')[1];
  const socket = new WebSocket(`ws://127.0.0.1:${info.port}/touch?token=${token}`, { origin });
  const messages = [];
  socket.on('message', bytes => {
    const m = JSON.parse(bytes); messages.push(m);
    if (m.type === 'ClockPing') socket.send(JSON.stringify({ type: 'ClockPong', seq: m.seq, clientReceiveUs: performance.now() * 1000, clientSendUs: performance.now() * 1000 }));
  });
  try {
    await once(socket, 'open'); socket.send(JSON.stringify({ type: 'Hello', protocolVersion: 'ring-touch-1', deviceName: 'test' }));
    await waitFor(messages, 'Pair');
    while (!host.info().devices[0]?.model || host.info().devices[0].model.sampleCount < 3) await new Promise(r => setTimeout(r, 20));
    const c = emptyChart(); addNote(c, { tick: 1920, point: { x: 0, y: 0 } });
    await host.command({ type: 'scene', chart: c });
    const now = performance.now() * 1000;
    await host.command({ type: 'transport', anchor: { state: 'playing', songUs: 0, pcMonoUs: now - 1000000 }, discontinuity: true });
    const { snapshot } = await host.command({ type: 'record-begin' });
    socket.send(JSON.stringify({ type: 'TouchBatch', sessionId: snapshot.sessionId, takeId: snapshot.takeId, generation: snapshot.generation, seq: 0,
      activeTouches: 5, samples: [{ sampleIndex: 0, touchId: 1, phase: 'began', startMonoUs: performance.now() * 1000,
        x: .5, y: .5, rawX: 500, rawY: 200, viewport: { width: 1000, height: 562.5 }, canvasVersion: snapshot.canvasVersion,
        transformVersion: 1, displaySongUs: 1e6, pose: identity(), clockModelId: host.info().devices[0].model.id }] }));
    const ack = await waitFor(messages, 'Ack'); assert.equal(ack.accepted, 1); assert.equal(persisted.length, 1);
    assert.equal(persisted[0].samples.length, 1); assert.equal(host.info().devices[0].peakTouches, 5);
    socket.close(); await once(socket, 'close');
    while (!events.some(x => x.type === 'take-ended')) await new Promise(r => setTimeout(r, 10));
    assert.match(events.find(x => x.type === 'take-ended').take.reason, /断线/);
  } finally { socket.terminate(); await host.stop(); }
});
test('bad pairing token and foreign Origin cannot open recording WebSocket', async () => {
  const host = new TabletHost({ root }); const info = await host.start(0, '127.0.0.1');
  try {
    for (const [token, origin] of [['wrong', `http://127.0.0.1:${info.port}`], [info.urls[0].split('#')[1], 'http://evil.invalid']]) {
      const socket = new WebSocket(`ws://127.0.0.1:${info.port}/touch?token=${token}`, { origin });
      const error = await once(socket, 'error'); assert.ok(error); socket.terminate();
    }
    assert.equal(host.info().devices.length, 0);
  } finally { await host.stop(); }
});
test('atomic Unicode path writes replace original without truncation or leftover temporary file', async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'ring-editor-test-'));
  try {
    const file = path.join(dir, '中文工程.ringproject'); await atomicWrite(file, 'first'); await atomicWrite(file, '第二版\n');
    assert.equal(await fs.readFile(file, 'utf8'), '第二版\n'); assert.deepEqual(await fs.readdir(dir), ['中文工程.ringproject']);
  } finally { await fs.rm(dir, { recursive: true, force: true }); }
});
