import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { emptyChart, tempoMap, cameraAt, projectPoint, unprojectPoint, addNote, moveTarget, validateChart, parseStrict, exportChart, quantize } from '../src/core/chart.mjs';
import { createProject, History, importTake } from '../src/core/project.mjs';
const sample = JSON.parse(await fs.readFile(new URL('../assets/sample-chart.json', import.meta.url), 'utf8'));

test('current game sample passes strict export and round trips all 38 notes', () => {
  assert.equal(validateChart(sample, { duration: 36 }).filter(x => x.severity === 'error').length, 0);
  assert.deepEqual(parseStrict(exportChart(sample, { duration: 36 })), sample);
});
test('tempo integration and inverse across 120 -> 150 BPM, offset, and boundary', () => {
  const map = tempoMap({ ppq: 960, offsetUs: -250000, tempos: [{ tick: 0, bpm: 120 }, { tick: 30720, bpm: 150 }] });
  assert.equal(map.tickToSeconds(30720), 15.75);
  assert.equal(map.tickToSeconds(32640), 16.55);
  for (const tick of [0, 1, 30000, 30720, 30721, 60000]) assert.ok(Math.abs(map.secondsToTick(map.tickToSeconds(tick)) - tick) < 1e-8);
});
test('camera absolute-time smooth interpolation, instant assignment, and inverse projection', () => {
  const c = emptyChart();
  c.actions = [{ id: 'a', eventType: 'MoveCamera', tick: 960, durationTicks: 1920, position: { x: 20, y: -10 }, rotation: 90, scale: 2, ease: 'smooth' }];
  const p = cameraAt(c, 1);
  assert.deepEqual(p, { position: { x: 10, y: -5 }, rotation: 45, scale: 1.5 });
  const point = { x: -35, y: 8 }, inverse = unprojectPoint(projectPoint(point, p), p);
  assert.ok(Math.abs(inverse.x - point.x) < 1e-10); assert.ok(Math.abs(inverse.y - point.y) < 1e-10);
  c.actions[0].durationTicks = 0; assert.equal(cameraAt(c, .5).scale, 2);
});
test('moving target keeps arrival path endpoint attached, copy ID remains unique and chord updates', () => {
  const c = emptyChart();
  const n = addNote(c, { tick: 1920, point: { x: 0, y: 0 }, motion: 'arrival' });
  moveTarget(c, n, { x: 12, y: -4 });
  assert.deepEqual(c.paths[0].end, n.target);
  const m = addNote(c, { tick: 1920, point: { x: -20, y: 0 } });
  assert.notEqual(n.id, m.id); assert.equal(c.settings.requiredTouches, 2);
});
test('strict parser rejects duplicate nested keys, bad JSON and depth > 64', () => {
  assert.throws(() => parseStrict('{"settings":{"title":"a","title":"b"}}'), /重复/);
  assert.throws(() => parseStrict('{"a":1,}'));
  assert.throws(() => parseStrict('['.repeat(65) + '0' + ']'.repeat(65)), /深度/);
});
test('unknown fields, insufficient touches, malformed path, overlap, music tail block export', () => {
  for (const mutate of [c => c.notes[0].radius = 0, c => c.settings.requiredTouches = 1,
    c => c.actions[1].tick = c.actions[0].tick, c => c.paths[0].end.x += 1, c => c.notes[0].surprise = true,
    c => c.notes[0].target = null, c => c.notes[0].pathId = null]) {
    const c = structuredClone(sample); mutate(c); assert.ok(validateChart(c, { duration: 36 }).some(x => x.severity === 'error')); assert.throws(() => exportChart(c, { duration: 36 }));
  }
  assert.throws(() => exportChart(sample, { duration: 4 }), /音频/);
});
test('undo/redo restores BPM and entire batch; new edits invalidate redo', () => {
  const h = new History(createProject(sample));
  h.commit('bpm', p => p.chart.timebase.tempos[0].bpm = 100); h.undo(); assert.equal(h.value.chart.timebase.tempos[0].bpm, 120);
  h.redo(); assert.equal(h.value.chart.timebase.tempos[0].bpm, 100); h.undo();
  h.commit('new', p => p.chart.settings.title = 'new'); assert.equal(h.redo(), false);
});
test('raw Take remains unchanged; explicit quantization preserves simultaneous inputs and supports free tick', () => {
  const c = emptyChart();
  const take = { samples: [{ songUs: 1000200, chartPoint: { x: -20, y: 0 } }, { songUs: 1000300, chartPoint: { x: 20, y: 0 } }] };
  const raw = structuredClone(take); const h = new History(createProject(c));
  h.commit('take', p => importTake(p.chart, take, { division: 4 }));
  assert.equal(h.value.chart.settings.requiredTouches, 2); assert.equal(h.value.chart.notes[0].tick, 1920);
  h.undo(); assert.equal(h.value.chart.notes.length, 0); h.redo(); assert.equal(h.value.chart.notes.length, 2);
  assert.deepEqual(take, raw); assert.equal(quantize(1993.3, 0), 1993);
});
test('instant cameras cannot share a start tick; float32 overflow/underflow cannot export to Unity', () => {
  const c = structuredClone(sample); c.actions[0].durationTicks = 0; c.actions[1].durationTicks = 0; c.actions[1].tick = c.actions[0].tick;
  assert.throws(() => exportChart(c), /共用/);
  for (const radius of [1e50, 1e-100]) { const n = structuredClone(sample); n.notes[0].radius = radius; assert.throws(() => exportChart(n), /半径/); }
});
