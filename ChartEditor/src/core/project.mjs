import { clone, emptyChart, tempoMap, quantize, addNote } from './chart.mjs';
export const PROJECT_VERSION = 'ring-editor-1';
export function createProject(chart = emptyChart(), audio = null) {
  return { projectVersion: PROJECT_VERSION, chart: clone(chart), audio, takes: [], author: '', loop: { enabled: false, a: 0, b: 8 } };
}
export function validateProject(p) {
  if (p?.projectVersion !== PROJECT_VERSION || !p.chart || !Array.isArray(p.takes)) throw new Error('不是支持的制谱器工程');
  if (p.audio && (typeof p.audio.name !== 'string' || typeof p.audio.base64 !== 'string' || p.audio.base64.length > 180 * 1024 * 1024)) throw new Error('工程音频数据无效或过大');
  if (p.takes.length > 100) throw new Error('工程最多保留 100 轮 Take，请分工程管理');
  for (const t of p.takes) {
    if (!t || typeof t.id !== 'string' || !Array.isArray(t.samples) || t.samples.length > 10000) throw new Error('工程包含无效的录入 Take');
    for (const s of t.samples) if (!s || !Number.isFinite(s.songUs) || !Number.isFinite(s.chartPoint?.x) || !Number.isFinite(s.chartPoint?.y)) throw new Error('工程包含无效的触控样本');
  }
  return p;
}
export class History {
  constructor(project) { this.value = clone(project); this.undoStack = []; this.redoStack = []; this.revision = 0; }
  commit(label, change) {
    const before = this.value;
    // Audio and raw Takes are immutable during edits; avoid cloning megabytes on every command.
    const next = { ...before, chart: clone(before.chart), loop: clone(before.loop), takes: before.takes.slice() };
    change(next);
    const changed = JSON.stringify(before.chart) !== JSON.stringify(next.chart) || before.audio !== next.audio ||
      before.author !== next.author || JSON.stringify(before.loop) !== JSON.stringify(next.loop) ||
      before.takes.length !== next.takes.length || before.takes.some((t, i) => t !== next.takes[i]);
    if (!changed) return false;
    this.undoStack.push({ label, value: before });
    if (this.undoStack.length > 100) this.undoStack.shift();
    this.value = next; this.redoStack = []; this.revision++; return true;
  }
  undo() {
    if (!this.undoStack.length) return false;
    const previous = this.undoStack.pop();
    this.redoStack.push({ label: previous.label, value: this.value }); this.value = previous.value; this.revision++; return true;
  }
  redo() {
    if (!this.redoStack.length) return false;
    const next = this.redoStack.pop();
    this.undoStack.push({ label: next.label, value: this.value }); this.value = next.value; this.revision++; return true;
  }
}
// A Take remains raw and unchanged. Quantization is an explicit, undoable editor transaction.
export function importTake(chart, take, { division = 4, calibrationMs = 0, motion = 'shrink', radius = 6, preRoll = 1920 } = {}) {
  const map = tempoMap(chart.timebase), added = [];
  for (const sample of take.samples) {
    const seconds = sample.songUs / 1e6 + calibrationMs / 1000;
    const tick = quantize(map.secondsToTick(seconds), division);
    if (tick < 1) continue;
    added.push(addNote(chart, { tick, point: sample.chartPoint, motion, radius, preRoll }).id);
  }
  return added;
}
