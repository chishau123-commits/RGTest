import test from 'node:test';
import assert from 'node:assert/strict';
import { emptyChart, addNote, cameraAt, validateChart } from '../src/core/chart.mjs';
import { addCamera } from '../src/core/authoring.mjs';
import { History, createProject } from '../src/core/project.mjs';
import { layoutTimeline, hitTimeline, beginTimelineGesture, moveTimelineGesture, commitTimelineGesture, scrubSeconds } from '../src/ui/timeline.mjs';

function fixture() {
  const c = emptyChart();
  addNote(c, { tick: 3840, preRoll: 1920, point: { x: 0, y: 0 }, motion: 'arrival' });
  addCamera(c, { tick: 5760, durationTicks: 1920, pose: cameraAt(c, 0) });
  return c;
}
function gesture(c, kind, mode, pointerSeconds = 1.5) {
  const l = layoutTimeline(c, 100), bar = l.bars.find(b => b.kind === kind);
  return beginTimelineGesture(c, { bar, mode }, pointerSeconds, 4);
}

test('note range shows spawn to hit, camera range shows full duration, and edge/body hit areas agree', () => {
  const l = layoutTimeline(fixture(), 100), n = l.bars.find(b => b.kind === 'note'), a = l.bars.find(b => b.kind === 'camera');
  assert.equal(n.right - n.left, 100); assert.equal(a.right - a.left, 100);
  for (const b of [n, a]) {
    const y = (b.top + b.bottom) / 2;
    assert.equal(hitTimeline(l, b.left + 2, y).mode, 'start');
    assert.equal(hitTimeline(l, b.right - 2, y).mode, 'end');
    assert.equal(hitTimeline(l, (b.left + b.right) / 2, y).mode, 'move');
    assert.equal(hitTimeline(l, (b.left + b.right) / 2, y).bar.id, b.id);
  }
  assert.equal(hitTimeline(l, n.left, 8).mode, 'scrub');
});

test('simultaneous and overlapping note ranges occupy separate selectable lanes without a finger cap', () => {
  const c = emptyChart();
  for (let i = 0; i < 17; i++) addNote(c, { tick: 3840, point: { x: i, y: 0 } });
  const l = layoutTimeline(c, 80);
  assert.equal(new Set(l.bars.map(b => b.lane)).size, 17);
  for (const b of l.bars) assert.equal(hitTimeline(l, (b.left + b.right) / 2, b.top + 10).bar.id, b.id);
});

test('middle drag keeps grab offset and musical duration, clamps at zero, and does not mutate original', () => {
  const c = fixture(), before = structuredClone(c), g = gesture(c, 'note', 'move');
  assert.equal(moveTimelineGesture(g, 1.5).changed, false);
  const r = moveTimelineGesture(g, 2);
  assert.equal(r.start, 2880); assert.equal(r.end, 4800);
  const clamped = moveTimelineGesture(g, -10);
  assert.equal(clamped.start, 0); assert.equal(clamped.end, 1920);
  assert.deepEqual(c, before); assert.deepEqual(r.chart.paths, before.paths);
});

test('note edges change pre-read/hit independently and never invert or become zero length', () => {
  const c = fixture(), left = gesture(c, 'note', 'start'), right = gesture(c, 'note', 'end');
  let r = moveTimelineGesture(left, 2); assert.equal(r.start, 2880); assert.equal(r.end, 3840);
  r = moveTimelineGesture(right, 2); assert.equal(r.start, 1920); assert.equal(r.end, 4800);
  r = moveTimelineGesture(left, 50); assert.equal(r.start, 3839); assert.equal(r.end, 3840);
  r = moveTimelineGesture(right, -50); assert.equal(r.start, 1920); assert.equal(r.end, 1921);
});

test('camera left edge preserves end, right preserves start, conflict rejects history and successful drag is one undo', () => {
  const c = fixture(), h = new History(createProject(c)), before = structuredClone(h.value);
  const left = gesture(c, 'camera', 'start'), right = gesture(c, 'camera', 'end');
  const l = moveTimelineGesture(left, 2), r = moveTimelineGesture(right, 2);
  assert.equal(l.start, 6720); assert.equal(l.end, 7680);
  assert.equal(r.start, 5760); assert.equal(r.end, 8640);
  h.commit('drag', p => commitTimelineGesture(p.chart, left, l)); assert.equal(h.undoStack.length, 1);
  assert.deepEqual(validateChart(h.value.chart).filter(i => i.severity === 'error'), []);
  h.undo(); assert.deepEqual(h.value, before); h.redo(); assert.equal(h.value.chart.actions[0].tick, 6720);
  addCamera(c, { tick: 8640, durationTicks: 960, pose: cameraAt(c, 0) });
  const conflicted = new History(createProject(c)), g = gesture(c, 'camera', 'end'), result = moveTimelineGesture(g, 3);
  assert.throws(() => conflicted.commit('drag', p => commitTimelineGesture(p.chart, g, result)), /冲突/);
  assert.equal(conflicted.undoStack.length, 0); assert.deepEqual(conflicted.value.chart, c);
});

test('musical delta and snapping remain correct when a drag crosses a BPM boundary', () => {
  const c = fixture(); c.timebase.tempos.push({ tick: 2880, bpm: 240 });
  const g = gesture(c, 'note', 'move', 1.4), r = moveTimelineGesture(g, 1.9);
  assert.equal(r.start, 3600); assert.equal(r.end, 5520);
});

test('scrubbing uses continuous seconds, clamps to audio bounds, and never changes chart history', () => {
  const h = new History(createProject(fixture())), l = layoutTimeline(h.value.chart, 80);
  assert.equal(scrubSeconds(l, 78 + 80 * 4.123, 36), 4.123);
  assert.equal(scrubSeconds(l, -900, 36), 0); assert.equal(scrubSeconds(l, 1e9, 36), 36);
  assert.equal(h.undoStack.length, 0);
});

test('off-grid note keeps its grab position on click and snaps its judgment time when moved', () => {
  const c = fixture(); c.notes[0].spawnTick += 17; c.notes[0].tick += 17;
  const g = gesture(c, 'note', 'move', 1.5);
  assert.equal(moveTimelineGesture(g, 1.5).changed, false);
  const r = moveTimelineGesture(g, 2);
  assert.equal(r.end, 4800); assert.equal(r.end % 240, 0); assert.equal(r.end - r.start, 1920);
});
