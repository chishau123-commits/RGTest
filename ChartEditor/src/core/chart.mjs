// Independent authoring implementation. No imports from the Unity project.
export const PPQ = 960;
export const WIDTH = 100 * 16 / 9;
export const SCHEMA = 'prototype-ring-0';
export const clone = value => structuredClone(value);
export const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
export const identity = () => ({ position: { x: 0, y: 0 }, rotation: 0, scale: 1 });

export function tempoMap(timebase) {
  const segments = [];
  let seconds = timebase.offsetUs / 1e6;
  for (const [i, t] of timebase.tempos.entries()) {
    if (i) seconds += (t.tick - timebase.tempos[i - 1].tick) * 60 / PPQ / timebase.tempos[i - 1].bpm;
    segments.push({ ...t, seconds });
  }
  return {
    segments,
    tickToSeconds(tick) {
      let s = segments[0];
      for (const next of segments) { if (next.tick > tick) break; s = next; }
      return s.seconds + (tick - s.tick) * 60 / PPQ / s.bpm;
    },
    secondsToTick(seconds) {
      let s = segments[0];
      for (const next of segments) { if (next.seconds > seconds) break; s = next; }
      return s.tick + (seconds - s.seconds) * PPQ * s.bpm / 60;
    }
  };
}

export function cameraAt(chart, seconds) {
  const map = tempoMap(chart.timebase);
  let pose = identity();
  for (const a of [...chart.actions].sort((a, b) => a.tick - b.tick || a.id.localeCompare(b.id))) {
    const start = map.tickToSeconds(a.tick), end = map.tickToSeconds(a.tick + a.durationTicks);
    if (seconds < start) break;
    const to = { position: a.position, rotation: a.rotation, scale: a.scale };
    if (seconds >= end) { pose = to; continue; }
    let u = (seconds - start) / (end - start);
    if (a.ease === 'smooth') u = u * u * (3 - 2 * u);
    const lerp = (x, y) => x + (y - x) * u;
    return { position: { x: lerp(pose.position.x, to.position.x), y: lerp(pose.position.y, to.position.y) },
      rotation: lerp(pose.rotation, to.rotation), scale: lerp(pose.scale, to.scale) };
  }
  return pose;
}
export function projectPoint(p, pose) {
  const a = pose.rotation * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
  return { x: pose.position.x + (p.x * c - p.y * s) * pose.scale,
    y: pose.position.y + (p.x * s + p.y * c) * pose.scale };
}
export function unprojectPoint(p, pose) {
  const a = pose.rotation * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
  const x = (p.x - pose.position.x) / pose.scale, y = (p.y - pose.position.y) / pose.scale;
  return { x: x * c + y * s, y: -x * s + y * c };
}
export const normalizedToView = (x, y) => ({ x: (x - .5) * WIDTH, y: (y - .5) * 100 });
export const quantize = (tick, division) => division ? Math.round(tick / (PPQ / division)) * (PPQ / division) : Math.round(tick);

export function emptyChart() {
  return { schemaVersion: SCHEMA, timebase: { ppq: PPQ, offsetUs: 0, tempos: [{ tick: 0, bpm: 120 }] },
    settings: { title: '未命名谱面', requiredTouches: 1 }, notes: [], paths: [], actions: [], decorations: [] };
}
export function uniqueId(chart, prefix = 'n') {
  const ids = new Set([...chart.notes, ...chart.paths, ...chart.actions].map(x => x.id));
  let i = 1;
  while (ids.has(`${prefix}${String(i).padStart(4, '0')}`)) i++;
  return `${prefix}${String(i).padStart(4, '0')}`;
}
export function addNote(chart, { tick, point, motion = 'shrink', radius = 6, preRoll = PPQ * 2, start }) {
  tick = Math.max(1, Math.round(tick));
  const note = { id: uniqueId(chart), tick, spawnTick: Math.max(0, tick - preRoll), motion,
    target: { ...point }, radius, pathId: '' };
  if (motion === 'arrival') {
    note.pathId = uniqueId(chart, 'p');
    chart.paths.push({ id: note.pathId, type: 'linear', start: start || { x: point.x - 30, y: point.y }, end: { ...point } });
  }
  chart.notes.push(note);
  updateTouches(chart);
  return note;
}
export function updateTouches(chart) {
  const counts = new Map();
  for (const n of chart.notes) counts.set(n.tick, (counts.get(n.tick) || 0) + 1);
  chart.settings.requiredTouches = Math.max(1, ...counts.values());
}
export function moveTarget(chart, note, point) {
  note.target = { ...point };
  const path = chart.paths.find(p => p.id === note.pathId);
  if (path) path.end = { ...point };
}

// Errors include stable JSON paths for manual inspection and UI navigation.
export function validateChart(chart, { duration = Infinity, allowEmpty = false } = {}) {
  const issues = [];
  const error = (code, path, message, objectId = '') => issues.push({ severity: 'error', code, path, message, objectId });
  const warning = (code, path, message, objectId = '') => issues.push({ severity: 'warning', code, path, message, objectId });
  const finite = x => typeof x === 'number' && Number.isFinite(x);
  const integer = x => Number.isSafeInteger(x);
  const float = x => finite(x) && Number.isFinite(Math.fround(x));
  const point = p => p && float(p.x) && float(p.y);
  const distance = (a, b) => Math.hypot(Math.fround(Math.fround(a.x) - Math.fround(b.x)), Math.fround(Math.fround(a.y) - Math.fround(b.y)));
  const keys = (obj, required, optional, path) => {
    if (!obj || typeof obj !== 'object' || Array.isArray(obj)) { error('E_FIELD', path, '需要对象'); return false; }
    for (const k of required) if (!Object.hasOwn(obj, k)) error('E_FIELD', `${path}.${k}`, '缺少必填字段');
    for (const k of Object.keys(obj)) if (![...required, ...optional].includes(k)) error('E_FIELD', `${path}.${k}`, '当前游戏不支持此字段');
    return true;
  };
  if (!keys(chart, ['schemaVersion', 'timebase', 'settings', 'notes', 'paths', 'actions', 'decorations'], [], '$')) return issues;
  if (chart.schemaVersion !== SCHEMA) error('E_VERSION', '$.schemaVersion', `仅支持 ${SCHEMA}`);
  if (keys(chart.timebase, ['ppq', 'offsetUs', 'tempos'], [], '$.timebase')) {
    if (chart.timebase.ppq !== PPQ) error('E_TIME', '$.timebase.ppq', 'PPQ 必须为 960');
    if (!integer(chart.timebase.offsetUs)) error('E_TIME', '$.timebase.offsetUs', '偏移必须为安全整数微秒');
    const ts = chart.timebase.tempos;
    if (!Array.isArray(ts) || !ts.length || ts.length > 10000) error('E_TEMPO', '$.timebase.tempos', '需要 1–10000 段 BPM');
    else ts.forEach((t, i) => {
      keys(t, ['tick', 'bpm'], [], `$.timebase.tempos[${i}]`);
      if (!t || !integer(t.tick) || (i === 0 ? t.tick !== 0 : t.tick <= ts[i - 1]?.tick) || !finite(t.bpm) || t.bpm <= 0)
        error('E_TEMPO', `$.timebase.tempos[${i}]`, '首段 tick=0，后续严格递增，BPM>0');
    });
  }
  if (keys(chart.settings, ['title', 'requiredTouches'], [], '$.settings')) {
    if (typeof chart.settings.title !== 'string' || !chart.settings.title.trim()) error('E_FIELD', '$.settings.title', '标题不能为空');
    if (!integer(chart.settings.requiredTouches) || chart.settings.requiredTouches <= 0 || chart.settings.requiredTouches > 2147483647) error('E_GROUP', '$.settings.requiredTouches', '触点数需要正的 32 位整数');
  }
  for (const [name, max] of [['notes', 10000], ['paths', 10000], ['actions', 2000], ['decorations', 0]]) {
    if (!Array.isArray(chart[name]) || chart[name].length > max) error('E_LIMIT', `$.${name}`, `需要数组，最多 ${max} 项`);
  }
  if (issues.length) return issues;
  if (!allowEmpty && !chart.notes.length) error('E_EMPTY', '$.notes', '至少需要一个音符才能导出给游戏');
  const ids = new Set(), pathMap = new Map(), chords = new Map();
  const id = (obj, path) => {
    if (typeof obj?.id !== 'string' || !obj.id.trim() || ids.has(obj.id)) error('E_ID', `${path}.id`, 'ID 不能为空或重复', obj?.id);
    else ids.add(obj.id);
  };
  const checkPoint = (p, path, objectId) => {
    if (!keys(p, ['x', 'y'], [], path) || !point(p)) error('E_GEOMETRY', path, '坐标必须为有限数字', objectId);
  };
  chart.paths.forEach((p, i) => {
    const at = `$.paths[${i}]`;
    if (!keys(p, ['id', 'type', 'start', 'end'], [], at)) return;
    id(p, at); checkPoint(p.start, `${at}.start`, p.id); checkPoint(p.end, `${at}.end`, p.id);
    if (p.type !== 'linear' || (point(p.start) && point(p.end) && distance(p.start, p.end) <= .0001))
      error('E_GEOMETRY', at, '目前仅支持起终点不同的直线路径', p.id);
    pathMap.set(p.id, p);
  });
  const map = tempoMap(chart.timebase);
  if (map.segments.some(s => !Number.isFinite(s.seconds))) error('E_TEMPO', '$.timebase.tempos', 'BPM 分段导致非有限的歌曲时间');
  chart.notes.forEach((n, i) => {
    const at = `$.notes[${i}]`;
    if (!keys(n, ['id', 'tick', 'spawnTick', 'motion', 'target', 'radius'], ['pathId'], at)) return;
    id(n, at); checkPoint(n.target, `${at}.target`, n.id);
    if (!integer(n.tick) || !integer(n.spawnTick) || n.spawnTick < 0 || n.tick <= n.spawnTick) error('E_TIME', at, '需 0≤spawnTick<tick，且为整数', n.id);
    if (!float(n.radius) || Math.fround(n.radius) <= 0) error('E_GEOMETRY', `${at}.radius`, '半径必须为游戏可表示的正数', n.id);
    if (!['shrink', 'arrival'].includes(n.motion)) error('E_FIELD', `${at}.motion`, '未知音符类型', n.id);
    if (Object.hasOwn(n, 'pathId') && typeof n.pathId !== 'string') error('E_FIELD', `${at}.pathId`, 'pathId 必须为字符串', n.id);
    if (n.motion === 'shrink' && n.pathId) error('E_REFERENCE', at, '缩圈音符不能引用路径', n.id);
    if (n.motion === 'arrival') {
      const p = pathMap.get(n.pathId);
      if (!p || !point(p.end) || !point(n.target) || distance(p.end, n.target) > .0001)
        error('E_REFERENCE', at, '到位路径必须存在，终点与目标一致', n.id);
    }
    const time = map.tickToSeconds(n.tick);
    if (!(time > map.tickToSeconds(n.spawnTick))) error('E_TIME', at, '预读与命中时间精度不足，不能表示为不同时刻', n.id);
    if (time < 0 || time >= duration || !Number.isFinite(time)) error('E_AUDIO_TIME', `${at}.tick`, '命中时刻必须在音频时长内', n.id);
    if (point(n.target) && finite(n.radius) && (Math.abs(n.target.x) + n.radius > WIDTH / 2 || Math.abs(n.target.y) + n.radius > 50))
      warning('W_OFFSCREEN', at, '接收圆可能超出舞台；请结合运镜检查', n.id);
    chords.set(n.tick, (chords.get(n.tick) || 0) + 1);
  });
  if (Math.max(0, ...chords.values()) > chart.settings.requiredTouches) error('E_GROUP', '$.settings.requiredTouches', '声明触点数少于同拍音符数');
  let lastEnd = -1, lastStart = -1;
  [...chart.actions].sort((a, b) => (a?.tick ?? 0) - (b?.tick ?? 0)).forEach(a => {
    const i = chart.actions.indexOf(a), at = `$.actions[${i}]`;
    if (!keys(a, ['id', 'eventType', 'tick', 'durationTicks', 'position', 'rotation', 'scale', 'ease'], [], at)) return;
    id(a, at); checkPoint(a.position, `${at}.position`, a.id);
    if (a.eventType !== 'MoveCamera' || !['linear', 'smooth'].includes(a.ease)) error('E_ACTION', at, '仅支持 MoveCamera 与 linear/smooth', a.id);
    if (!integer(a.tick) || !integer(a.durationTicks) || a.tick < 0 || a.durationTicks < 0 || !integer(a.tick + a.durationTicks) || a.tick < lastEnd || a.tick <= lastStart)
      error('E_ACTION', at, '相机动作必须整数时间，不能重叠或共用起点 tick', a.id);
    if (!float(a.rotation) || !float(a.scale) || Math.fround(a.scale) <= 0) error('E_TRANSFORM', at, '旋转与正缩放必须为游戏可表示的数字', a.id);
    const startTime = map.tickToSeconds(a.tick), endTime = map.tickToSeconds(a.tick + a.durationTicks);
    if (!Number.isFinite(startTime) || !Number.isFinite(endTime) || a.durationTicks > 0 && !(endTime > startTime)) error('E_TIME', at, '相机起止时间无法表示', a.id);
    lastStart = a.tick;
    lastEnd = a.tick + a.durationTicks;
  });
  if (new TextEncoder().encode(JSON.stringify(chart)).length > 1024 * 1024) error('E_LIMIT', '$', '谱面超出游戏 1 MiB 限制');
  return issues;
}

// JSON.parse accepts duplicate keys: reject them rather than silently losing author data.
export function parseStrict(text) {
  if (new TextEncoder().encode(text).length > 1024 * 1024) throw new Error('谱面超出 1 MiB');
  const tokens = text.match(/"(?:[^"\\]|\\.)*"|[{}\[\]:,]|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|true|false|null/g) || [];
  const stack = [];
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i];
    if (t === '{' || t === '[') { stack.push({ type: t, keys: new Set() }); if (stack.length > 64) throw new Error('JSON 深度超出 64'); }
    else if (t === '}' || t === ']') stack.pop();
    else if (t.startsWith('"') && tokens[i + 1] === ':' && stack.at(-1)?.type === '{') {
      const key = JSON.parse(t), scope = stack.at(-1);
      if (scope.keys.has(key)) throw new Error(`重复 JSON 字段：${key}`);
      scope.keys.add(key);
    }
  }
  return JSON.parse(text);
}

export function exportChart(chart, options) {
  const issues = validateChart(chart, options);
  if (issues.some(i => i.severity === 'error')) throw new Error(issues.filter(i => i.severity === 'error').map(i => `${i.path}: ${i.message}`).join('\n'));
  const result = clone(chart);
  for (const key of ['notes', 'actions']) result[key].sort((a, b) => a.tick - b.tick || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  result.paths.sort((a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
  const text = JSON.stringify(result, null, 2) + '\n';
  if (new TextEncoder().encode(text).length > 1024 * 1024) throw new Error('格式化后的谱面超出游戏 1 MiB 限制');
  return text;
}
