import test from 'node:test';
import assert from 'node:assert/strict';
import { ClockSync, RecordingSession } from '../src/recording/session.mjs';
import { emptyChart, cameraAt, projectPoint, WIDTH } from '../src/core/chart.mjs';

function fixture() {
  let now = 5e6;
  const s = new RecordingSession(() => now), c = emptyChart();
  c.actions = [{ id: 'cam', eventType: 'MoveCamera', tick: 0, durationTicks: 0, position: { x: 10, y: 5 }, rotation: 35, scale: 1.5, ease: 'linear' }];
  s.setScene(c); s.setTransport({ state: 'playing', songUs: 0, pcMonoUs: 1e6 }); s.begin();
  const clock = new ClockSync();
  for (let i = 0; i < 4; i++) clock.add({ pcSendUs: 1e6 + i * 1000, clientReceiveUs: 500000 + i * 1000 + 100, clientSendUs: 500000 + i * 1000 + 100, pcReceiveUs: 1e6 + i * 1000 + 200 });
  clock.model.measuredPcUs = now;
  const device = { id: 'tablet', clock };
  const pose = cameraAt(c, 3), point = projectPoint({ x: 20, y: -10 }, pose);
  const batch = seq => ({ sessionId: s.sessionId, takeId: s.take.id, generation: s.generation, seq,
    samples: [{ sampleIndex: 0, touchId: 9, phase: 'began', startMonoUs: 3500000, x: point.x / WIDTH + .5, y: point.y / 100 + .5,
      rawX: 200, rawY: 120, viewport: { width: 1000, height: 562.5 }, canvasVersion: s.canvasVersion, transformVersion: 7, displaySongUs: 3e6, pose, clockModelId: clock.model.id }] });
  return { s, device, batch, setNow: n => now = n };
}
test('NTP midpoint ignores processing delay and fits low RTT clock; invalid pong is rejected', () => {
  const clock = new ClockSync();
  const m = clock.add({ pcSendUs: 1000000, clientReceiveUs: 500100, clientSendUs: 500150, pcReceiveUs: 1000250 });
  assert.equal(m.b, 500000); assert.equal(m.rttUs, 200); assert.equal(m.uncertaintyUs, 100);
  assert.throws(() => clock.add({ pcSendUs: 10, clientReceiveUs: 20, clientSendUs: 10, pcReceiveUs: 20 }));
});
test('arrival time does not set note time; inverse uses tablet displayed pose and retains raw clock model', () => {
  const { s, device, batch } = fixture(); s.acceptBatch(batch(0), device);
  const sample = s.take.samples[0];
  assert.equal(sample.songUs, 3e6); assert.equal(sample.delayedUs, 1e6);
  assert.ok(Math.abs(sample.chartPoint.x - 20) < 1e-8); assert.ok(Math.abs(sample.chartPoint.y + 10) < 1e-8);
  assert.equal(sample.clockModel.id, device.clock.model.id); assert.equal(sample.transformVersion, 7);
});
test('repeated batches deduplicate; ACK only advances contiguous sequence after gap fills', () => {
  const { s, device, batch } = fixture();
  assert.equal(s.acceptBatch(batch(1), device).contiguousSeq, -1);
  assert.equal(s.acceptBatch(batch(1), device).duplicate, true);
  assert.equal(s.acceptBatch(batch(0), device).contiguousSeq, 1);
  assert.equal(s.take.samples.length, 2);
});
test('Seek invalidates old generation; old samples never enter new Take', () => {
  const { s, device, batch } = fixture(), old = batch(0);
  s.setTransport({ state: 'playing', songUs: 2e6, pcMonoUs: 5e6 }, true); s.begin();
  s.acceptBatch(old, device); assert.equal(s.take.samples.length, 0);
  assert.equal(s.retired.get(old.takeId).samples.length, 1);
});
test('stale clock, unsupported phase, wrong pose, unknown canvas, and far-future samples are rejected', () => {
  const { s, device, batch } = fixture();
  for (const mutate of [b => b.samples[0].phase = 'moved', b => b.samples[0].pose.scale = 2, b => b.samples[0].canvasVersion++, b => b.seq = 300]) {
    const b = batch(0); b.samples[0].pose = structuredClone(b.samples[0].pose); mutate(b); assert.throws(() => s.acceptBatch(b, device));
  }
  device.clock.model.measuredPcUs = -20e6; assert.throws(() => s.acceptBatch(batch(0), device), /CLOCK/);
});
test('countdown contacts skipped; ending Take retains raw samples and reason', () => {
  const { s, device, batch } = fixture(), b = batch(0); b.samples[0].startMonoUs = 100;
  s.acceptBatch(b, device); assert.equal(s.take.samples.length, 0); assert.equal(s.take.skipped, 1);
  const stopped = s.end('断线'); assert.equal(stopped.reason, '断线'); assert.equal(stopped.status, 'stopped'); assert.equal(s.take, null);
});
test('late pre-stop contacts backfill only their original Take; post-stop contacts are skipped', () => {
  const { s, device, batch, setNow } = fixture();
  const late = batch(0); s.end('stop'); setNow(6e6); device.clock.model.measuredPcUs = 6e6;
  const ack = s.acceptBatch(late, device); assert.equal(ack.historical, true); assert.equal(ack.accepted, 1);
  const after = structuredClone(late); after.seq = 1; after.samples[0].startMonoUs = 5100000;
  assert.equal(s.acceptBatch(after, device).accepted, 0); assert.equal(s.retired.get(late.takeId).samples.length, 1);
});
test('capture clock model is retained when later clock fits change', () => {
  const { s, device, batch } = fixture(); const original = structuredClone(device.clock.model), b = batch(0);
  device.models = new Map([[original.id, original]]);
  device.clock.model = { ...original, id: 'new-model', b: original.b + 2000 };
  s.acceptBatch(b, device); assert.equal(s.take.samples[0].songUs, 3e6); assert.equal(s.take.samples[0].clockModel.id, original.id);
});
test('drift fit after 20 seconds estimates a 100 ppm monotonic clock rate', () => {
  const clock = new ClockSync();
  for (let i = 0; i < 5; i++) {
    const client = 1e6 + i * 10e6, pc = client * 1.0001 + 900000;
    clock.add({ pcSendUs: pc - 100, pcReceiveUs: pc + 100, clientReceiveUs: client, clientSendUs: client });
  }
  assert.ok(Math.abs(clock.model.a - 1.0001) < 1e-10); assert.ok(clock.model.uncertaintyUs < 101);
});
test('simultaneous 17-touch batch is not capped to two or four fingers', () => {
  const { s, device, batch } = fixture(), b = batch(0), sample = b.samples[0];
  b.samples = Array.from({ length: 17 }, (_, i) => ({ ...structuredClone(sample), sampleIndex: i, touchId: i }));
  assert.equal(s.acceptBatch(b, device).accepted, 17); assert.equal(s.take.samples.length, 17);
});
