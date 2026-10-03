import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { randomUUID } from 'node:crypto';
import { TakeStore } from '../src/recording/take-store.mjs';

test('raw sample journal survives interruption, preserves samples and finishes as full JSON', async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'ring-take-store-'));
  try {
    const store = new TakeStore(dir), t = { id: randomUUID(), status: 'recording', startedUs: 1, samples: [], batches: new Map() };
    t.samples.push({ deviceId: 't', seq: 0, sampleIndex: 0, songUs: 100, chartPoint: { x: 0, y: 0 } });
    await store.save(t); await store.save(t);
    assert.equal((await new TakeStore(dir).recover())[0].samples.length, 1);
    assert.equal((await store.recover())[0].status, 'interrupted');
    t.samples.push({ deviceId: 't', seq: 1, sampleIndex: 0, songUs: 200, chartPoint: { x: 1, y: 2 } });
    await store.save(t); t.status = 'stopped'; await store.save(t);
    const full = JSON.parse(await fs.readFile(path.join(dir, `${t.id}.take.json`), 'utf8'));
    assert.equal(full.samples.length, 2); assert.equal(Object.hasOwn(full, 'batches'), false);
  } finally { await fs.rm(dir, { recursive: true, force: true }); }
});
test('crash-truncated final journal line is ignored; complete recorded prefix is recovered', async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'ring-take-tail-'));
  try {
    const store = new TakeStore(dir), t = { id: randomUUID(), status: 'recording', samples: [{ deviceId: 't', seq: 0, sampleIndex: 0 }] };
    await store.save(t); await fs.appendFile(path.join(dir, `${t.id}.take-journal.jsonl`), '{"deviceId":');
    assert.equal((await store.recover())[0].samples.length, 1);
  } finally { await fs.rm(dir, { recursive: true, force: true }); }
});
