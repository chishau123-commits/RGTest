import { PPQ, clone, tempoMap, quantize, updateTouches } from '../core/chart.mjs';
import { setCameraTiming } from '../core/authoring.mjs';

export const TIMELINE = Object.freeze({ origin: 78, ruler: 30, waveTop: 34, waveBottom: 82, laneHeight: 32, barHeight: 22, handle: 8 });

// One geometry model drives both drawing and hit testing. Overlapping ranges get separate lanes.
export function layoutTimeline(chart, pps) {
  const map = tempoMap(chart.timebase), bars = [], sections = [];
  const xAt = tick => TIMELINE.origin + map.tickToSeconds(tick) * pps;
  let y = 86;
  for (const [kind, objects] of [['note', chart.notes], ['camera', chart.actions]]) {
    const lanes = [], top = y;
    y += 22;
    const sorted = objects.map(object => ({ object, start: kind === 'note' ? object.spawnTick : object.tick,
      end: kind === 'note' ? object.tick : object.tick + object.durationTicks }))
      .sort((a, b) => a.start - b.start || a.end - b.end || a.object.id.localeCompare(b.object.id));
    for (const { object, start, end } of sorted) {
      const left = xAt(start), right = Math.max(left + 22, xAt(end));
      let lane = lanes.findIndex(lastRight => lastRight + 4 <= left);
      if (lane < 0) lane = lanes.length;
      lanes[lane] = right;
      bars.push({ id: object.id, kind, motion: object.motion, start, end, left, right, top: y + lane * TIMELINE.laneHeight,
        bottom: y + lane * TIMELINE.laneHeight + TIMELINE.barHeight, lane });
    }
    y += Math.max(1, lanes.length) * TIMELINE.laneHeight + 8;
    sections.push({ kind, top, bottom: y, lanes: Math.max(1, lanes.length) });
  }
  return { map, pps, bars, sections, height: y + 4, xAt };
}

export function hitTimeline(layout, x, y, { selected = null, song = 0 } = {}) {
  // Ruler/playhead cap and waveform are dedicated scrub surfaces.
  if (y <= TIMELINE.waveBottom) return { mode: 'scrub' };
  const candidates = layout.bars.filter(b => x >= b.left - 2 && x <= b.right + 2 && y >= b.top - 3 && y <= b.bottom + 3)
    .sort((a, b) => Number(b.id === selected) - Number(a.id === selected));
  const bar = candidates[0];
  if (bar) return { mode: x <= bar.left + TIMELINE.handle ? 'start' : x >= bar.right - TIMELINE.handle ? 'end' : 'move', bar };
  if (Math.abs(x - (TIMELINE.origin + song * layout.pps)) <= 8) return { mode: 'scrub' };
  return { mode: 'scrub' };
}

export function beginTimelineGesture(chart, hit, pointerSeconds, division) {
  const map = tempoMap(chart.timebase), bar = hit.bar;
  if (!bar) return { mode: 'scrub' };
  return { mode: hit.mode, id: bar.id, kind: bar.kind, start: bar.start, end: bar.end, original: clone(chart), map,
    pointerTick: map.secondsToTick(pointerSeconds), division };
}

export function moveTimelineGesture(gesture, pointerSeconds) {
  const { map, mode, start, end, kind, division } = gesture;
  const rawDelta = map.secondsToTick(pointerSeconds) - gesture.pointerTick;
  const snapAnchor = mode === 'end' || mode === 'move' && kind === 'note' ? end : start;
  const delta = Math.abs(rawDelta) < 1e-7 ? 0 : quantize(snapAnchor + rawDelta, division) - snapAnchor;
  const minLength = kind === 'note' ? 1 : 0;
  let nextStart = start, nextEnd = end;
  if (mode === 'move') { nextStart = Math.max(0, start + delta); nextEnd = nextStart + (end - start); }
  else if (mode === 'start') nextStart = Math.max(0, Math.min(end - minLength, start + delta));
  else if (mode === 'end') nextEnd = Math.max(start + minLength, end + delta);
  if (![nextStart, nextEnd].every(Number.isSafeInteger)) throw new Error('时间范围超出安全整数');
  const chart = clone(gesture.original), object = (kind === 'note' ? chart.notes : chart.actions).find(o => o.id === gesture.id);
  if (kind === 'note') { object.spawnTick = nextStart; object.tick = nextEnd; updateTouches(chart); }
  else { object.tick = nextStart; object.durationTicks = nextEnd - nextStart; }
  return { chart, start: nextStart, end: nextEnd, changed: nextStart !== start || nextEnd !== end };
}

export function commitTimelineGesture(chart, gesture, result) {
  if (gesture.kind === 'camera') setCameraTiming(chart, gesture.id, { tick: result.start, durationTicks: result.end - result.start });
  else {
    const note = chart.notes.find(n => n.id === gesture.id);
    if (!note) throw new Error('选中音符已不存在');
    if (!Number.isSafeInteger(result.start) || !Number.isSafeInteger(result.end) || result.start < 0 || result.end <= result.start) throw new Error('音符需 0≤出现 tick<判定 tick');
    note.spawnTick = result.start; note.tick = result.end; updateTouches(chart);
  }
}

export function scrubSeconds(layout, x, duration) {
  return Math.max(0, Math.min(duration || 600, (x - TIMELINE.origin) / layout.pps));
}

export const gridStep = division => division ? PPQ / division : 1;
