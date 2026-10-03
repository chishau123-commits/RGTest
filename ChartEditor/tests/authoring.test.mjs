import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { cameraAt, clone, validateChart, exportChart } from '../src/core/chart.mjs';
import { createProject, History } from '../src/core/project.mjs';
import { addCamera, setCameraTiming, planCameraRepair } from '../src/core/authoring.mjs';
const sample = JSON.parse(await fs.readFile(new URL('../assets/sample-chart.json', import.meta.url), 'utf8'));
const cameraErrors = c => validateChart(c, { duration: 36 }).filter(x => x.severity === 'error');

test('adding inside the final demo camera and repeated add cannot invalidate the authoring history', () => {
  const h = new History(createProject(sample)), before = clone(h.value);
  assert.throws(() => h.commit('camera', p => addCamera(p.chart, { tick: 72000, pose: cameraAt(p.chart, 33.2) })), /cam-7/);
  assert.deepEqual(h.value, before); assert.equal(h.undoStack.length, 0);
  let id;
  h.commit('camera', p => { id = addCamera(p.chart, { tick: 72960, pose: cameraAt(p.chart, 33.6) }).id; });
  assert.deepEqual(cameraErrors(h.value.chart), []);
  const valid = clone(h.value);
  assert.throws(() => h.commit('repeat', p => addCamera(p.chart, { tick: 72960, pose: cameraAt(p.chart, 33.6) })), new RegExp(id));
  assert.deepEqual(h.value, valid); assert.equal(h.undoStack.length, 1);
  h.undo(); assert.deepEqual(h.value, before);
});

test('property and timeline camera moves or duration edits reject conflicts atomically; adjacency is valid', () => {
  const h = new History(createProject(sample)), before = clone(h.value);
  for (const change of [{ tick: 19000 }, { durationTicks: 8000 }, { tick: -1 }, { durationTicks: -1 }]) {
    assert.throws(() => h.commit('timing', p => setCameraTiming(p.chart, 'cam-0', change)));
    assert.deepEqual(h.value, before);
  }
  h.commit('adjacent', p => setCameraTiming(p.chart, 'cam-0', { tick: 12000, durationTicks: 7200 }));
  assert.deepEqual(cameraErrors(h.value.chart), []);
});

test('instant actions cannot share a tick or sit inside an animation, but can sit at its end', () => {
  const c = clone(sample);
  assert.throws(() => addCamera(c, { tick: 72000, durationTicks: 0, pose: cameraAt(c, 33.2) }));
  addCamera(c, { tick: 72960, durationTicks: 0, pose: cameraAt(c, 33.6) });
  assert.throws(() => addCamera(c, { tick: 72960, durationTicks: 0, pose: cameraAt(c, 33.6) }));
  addCamera(c, { tick: 72961, durationTicks: 960, pose: cameraAt(c, 33.6) });
  assert.deepEqual(cameraErrors(c), []);
});

function brokenDraft() {
  const c = clone(sample);
  for (const [i, tick] of [72000, 72000, 72000, 75360, 75360, 75360].entries())
    c.actions.push({ id: `cam000${i + 1}`, eventType: 'MoveCamera', tick, durationTicks: 960,
      position: { x: .17318038715056705, y: -.06927215486022686 }, rotation: .519541161451702, scale: 1, ease: 'smooth' });
  return c;
}

test('recovered overlapping draft is repaired without losing poses, notes, audio or Takes; batch is undoable', () => {
  const c = brokenDraft(), original = clone(c), project = createProject(c, { name: 'music', base64: 'AA==' });
  project.takes.push({ id: 'raw', samples: [] });
  const h = new History(project), plan = planCameraRepair(c);
  assert.equal(cameraErrors(c).length, 5); assert.equal(plan.length, 6); assert.deepEqual(c, original);
  h.commit('repair', p => { for (const move of plan) p.chart.actions.find(a => a.id === move.id).tick = move.to; });
  assert.deepEqual(cameraErrors(h.value.chart), []);
  assert.doesNotThrow(() => exportChart(h.value.chart, { duration: 36 }));
  assert.deepEqual(h.value.chart.notes, original.notes); assert.deepEqual(h.value.chart.paths, original.paths);
  assert.deepEqual(h.value.chart.actions.map(({ tick, ...pose }) => pose), original.actions.map(({ tick, ...pose }) => pose));
  assert.deepEqual(h.value.audio, project.audio); assert.deepEqual(h.value.takes, project.takes);
  h.undo(); assert.deepEqual(h.value.chart, original); h.redo(); assert.deepEqual(cameraErrors(h.value.chart), []);
  assert.deepEqual(planCameraRepair(h.value.chart), []);
});

test('repair rejects malformed or overflowing timing rather than guessing or partially changing a draft', () => {
  for (const mutate of [c => c.actions[0].tick = -1, c => c.actions[0].durationTicks = NaN,
    c => { c.actions[0].tick = Number.MAX_SAFE_INTEGER; c.actions[0].durationTicks = 1; }]) {
    const c = clone(sample); mutate(c); const before = clone(c);
    assert.throws(() => planCameraRepair(c)); assert.deepEqual(c, before);
  }
});
