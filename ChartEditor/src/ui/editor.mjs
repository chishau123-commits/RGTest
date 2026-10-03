import { PPQ, WIDTH, clone, tempoMap, cameraAt, unprojectPoint, normalizedToView, quantize, addNote, uniqueId, moveTarget, updateTouches, validateChart, exportChart } from '../core/chart.mjs';
import { createProject, History, importTake } from '../core/project.mjs';
import { addCamera, setCameraTiming, planCameraRepair } from '../core/authoring.mjs';
import { AudioTransport } from './transport.mjs';
import { compilePreview, drawStage, fitCanvas, waveform } from './render.mjs';
import { TIMELINE, layoutTimeline, hitTimeline, beginTimelineGesture, moveTimelineGesture, commitTimelineGesture, scrubSeconds, gridStep } from './timeline.mjs';

const $ = id => document.getElementById(id), api = window.editorAPI || (await import('./preview-api.mjs')).previewAPI, stage = $('stage'), timeline = $('timeline');
const audio = new AudioTransport();
let history, preview, waves = null, selected = null, tool = 'select', dirty = false, issues = [], lastUI = 0, lastBeat = -1;
let tabletInfo = null, recording = false, hostOffsetUs = 0, hostClockUncertaintyUs = 0, activeTake = null, saving = false, drag = null;
let timelinePps = 80, lastAutosaveRevision = -1, recoveryOffer = null;
let timelineDrag = null, timelineLayout = null;
const pendingTakes = new Set();
const sha256 = async bytes => [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(b => b.toString(16).padStart(2, '0')).join('');
const current = () => history.value;
const chart = () => current().chart;
const setStatus = text => { $('status').textContent = text; };
const run = fn => async (...args) => { try { await fn(...args); } catch (e) { setStatus(e.message || String(e)); if (recording) await stopRecording().catch(() => {}); } };
function element(tag, text, className) { const e = document.createElement(tag); if (text != null) e.textContent = text; if (className) e.className = className; return e; }
async function confirm(title, message) {
  $('confirm-title').textContent = title; $('confirm-message').textContent = message; $('confirm').showModal();
  return new Promise(resolve => {
    const finish = value => { $('confirm').close(); $('confirm').oncancel = null; resolve(value); };
    $('confirm-no').onclick = () => finish(false); $('confirm-yes').onclick = () => finish(true);
    $('confirm').oncancel = e => { e.preventDefault(); finish(false); };
  });
}
function selectedObject() { return [...chart().notes, ...chart().actions].find(n => n.id === selected); }
function edit(label, change) {
  if (recording) { setStatus('请先停止录制，再编辑谱面'); return false; }
  if (timelineDrag) cancelTimelineDrag();
  if (history.commit(label, change)) { dirty = true; refresh(); return true; }
  return false;
}
function refresh() {
  issues = validateChart(chart(), { duration: audio.duration || Infinity });
  if (!issues.some(i => i.severity === 'error' && i.code !== 'E_EMPTY' && i.code !== 'E_AUDIO_TIME')) preview = compilePreview(chart());
  else {
    preview = null;
    if (audio.playing) { audio.pause(); publishTransport(true).catch(() => {}); }
    drawValidationError(); showPanel('issues');
  }
  $('chart-name').textContent = chart().settings.title;
  $('note-count').textContent = `${chart().notes.length} NOTES / ${chart().settings.requiredTouches} TOUCHES`;
  $('save-state').textContent = dirty ? '● 有未保存的编辑' : '工程已保存 / 原创演示';
  $('undo').disabled = !history.undoStack.length || recording; $('redo').disabled = !history.redoStack.length || recording;
  for (const id of ['loop', 'loop-a', 'loop-b']) $(id).disabled = recording;
  $('loop').checked = !!current().loop?.enabled; $('loop-a').value = current().loop?.a ?? 0; $('loop-b').value = current().loop?.b ?? 8;
  renderProperties(); renderTempos(); renderIssues(); renderTakes(); renderActions(); resizeTimeline();
  if (!recording && preview) syncScene().catch(e => setStatus(`平板暂未同步：${e.message}`));
}
function drawValidationError() {
  const ctx = stage.getContext('2d'); ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.fillStyle = '#111926'; ctx.fillRect(0, 0, stage.width, stage.height);
  ctx.fillStyle = '#f4c783'; ctx.font = `${16 * Math.min(devicePixelRatio || 1, 2)}px Microsoft YaHei`; ctx.textAlign = 'center';
  const first = issues.find(i => i.severity === 'error' && !['E_EMPTY', 'E_AUDIO_TIME'].includes(i.code));
  ctx.fillText('谱面存在错误；请查看下方“校验”', stage.width / 2, stage.height / 2 - 20, stage.width - 40);
  if (first) ctx.fillText(`${first.objectId || first.path}：${first.message}`, stage.width / 2, stage.height / 2 + 20, stage.width - 40);
}
function showPanel(panel) {
  document.querySelectorAll('[data-panel]').forEach(t => t.classList.toggle('active', t.dataset.panel === panel));
  for (const p of ['issues', 'takes', 'actions']) $(`${p}-panel`).hidden = panel !== p;
}
function field(parent, title, value, callback, { type = 'number', step = 'any', options, readonly = false } = {}) {
  const label = element('label', title), input = element(options ? 'select' : 'input');
  if (options) for (const [v, caption] of options) { const o = element('option', caption); o.value = v; input.append(o); }
  else { input.type = type; input.step = step; }
  input.value = value ?? ''; input.disabled = readonly || recording;
  input.onchange = run(async () => {
    const v = type === 'number' && !options ? Number(input.value) : input.value;
    if (type === 'number' && !options && !Number.isFinite(v)) { setStatus('请输入有限数字'); return; }
    callback(v);
  });
  label.append(input); parent.append(label); return input;
}
function renderProperties() {
  const parent = $('properties'); parent.replaceChildren();
  const obj = selectedObject(); $('selection-title').textContent = obj ? obj.id : '谱面设置';
  if (!obj) {
    field(parent, '标题', chart().settings.title, value => edit('修改标题', p => p.chart.settings.title = value), { type: 'text' });
    field(parent, '作者（仅工程）', current().author, value => edit('修改作者', p => p.author = value), { type: 'text' });
    field(parent, '拍零偏移 / ms', chart().timebase.offsetUs / 1000, value => edit('修改拍零偏移', p => p.chart.timebase.offsetUs = Math.round(value * 1000)));
    field(parent, '所需触点数', chart().settings.requiredTouches, value => edit('修改触点声明', p => p.chart.settings.requiredTouches = Math.round(value)), { step: '1' });
    parent.append(element('p', '先定位时间，再在舞台点击放置。选择后拖动目标；到位音符的黄色起点可拖动。', 'hint'));
    return;
  }
  const isNote = Object.hasOwn(obj, 'motion');
  const update = (label, change) => edit(label, p => { const n = (isNote ? p.chart.notes : p.chart.actions).find(x => x.id === selected); change(n, p.chart); });
  field(parent, 'ID', obj.id, () => {}, { type: 'text', readonly: true });
  field(parent, isNote ? '判定 tick' : '开始 tick', obj.tick, value => {
    try { update('修改音乐时刻', (n, c) => {
      if (isNote) { n.tick = Math.round(value); updateTouches(c); }
      else setCameraTiming(c, n.id, { tick: Math.round(value) });
    }); } finally { renderProperties(); }
  }, { step: '1' });
  parent.append(element('div', `${tempoMap(chart().timebase).tickToSeconds(obj.tick).toFixed(6)} 秒`, 'hint'));
  if (isNote) {
    field(parent, '出现 tick', obj.spawnTick, v => update('修改预读', n => n.spawnTick = Math.round(v)), { step: '1' });
    field(parent, '运动类型', obj.motion, v => update('修改运动类型', (n, c) => {
      if (n.motion === v) return;
      if (v === 'arrival') { n.pathId = uniqueId(c, 'p'); c.paths.push({ id: n.pathId, type: 'linear', start: { x: n.target.x - 30, y: n.target.y }, end: clone(n.target) }); }
      else { const old = n.pathId; n.pathId = ''; if (!c.notes.some(x => x.id !== n.id && x.pathId === old)) c.paths = c.paths.filter(x => x.id !== old); }
      n.motion = v;
    }), { type: 'text', options: [['shrink', '缩圈'], ['arrival', '到位']] });
    for (const axis of ['x', 'y']) field(parent, `目标 ${axis.toUpperCase()}`, obj.target[axis], v => update('移动目标', (n, c) => moveTarget(c, n, { ...n.target, [axis]: v })));
    field(parent, '半径', obj.radius, v => update('修改半径', n => n.radius = v));
    const path = chart().paths.find(x => x.id === obj.pathId);
    if (path) for (const axis of ['x', 'y']) field(parent, `路径起点 ${axis.toUpperCase()}`, path.start[axis], v => update('修改路径起点', (n, c) => c.paths.find(x => x.id === n.pathId).start[axis] = v));
  } else {
    field(parent, '持续 tick', obj.durationTicks, v => {
      try { update('修改运镜时长', (n, c) => setCameraTiming(c, n.id, { durationTicks: Math.round(v) })); }
      finally { renderProperties(); }
    }, { step: '1' });
    for (const axis of ['x', 'y']) field(parent, `目标平移 ${axis.toUpperCase()}`, obj.position[axis], v => update('修改运镜平移', n => n.position[axis] = v));
    field(parent, '目标旋转 / °', obj.rotation, v => update('修改运镜旋转', n => n.rotation = v));
    field(parent, '目标缩放', obj.scale, v => update('修改运镜缩放', n => n.scale = v));
    field(parent, '缓动', obj.ease, v => update('修改运镜缓动', n => n.ease = v), { type: 'text', options: [['linear', '线性'], ['smooth', '平滑']] });
  }
}
function renderTempos() {
  $('tempos').replaceChildren();
  chart().timebase.tempos.forEach((t, i) => {
    const row = element('div', null, 'tempo-row');
    const tick = element('input'), bpm = element('input'), remove = element('button', '×');
    tick.type = bpm.type = 'number'; tick.value = t.tick; bpm.value = t.bpm; tick.title = '起始 tick'; bpm.title = 'BPM';
    tick.disabled = recording || i === 0; bpm.disabled = remove.disabled = recording; if (i === 0) remove.disabled = true;
    const apply = () => {
      const nextTick = Number(tick.value), nextBpm = Number(bpm.value);
      if (!Number.isSafeInteger(nextTick) || nextTick < 0 || !Number.isFinite(nextBpm) || nextBpm <= 0) {
        setStatus('BPM 需要有限正数，分段 tick 需要非负安全整数'); renderTempos(); return;
      }
      edit('修改 BPM（保持 tick）', p => {
        p.chart.timebase.tempos[i] = { tick: nextTick, bpm: nextBpm };
        p.chart.timebase.tempos.sort((a, b) => a.tick - b.tick);
      });
    };
    tick.onchange = bpm.onchange = apply;
    remove.onclick = () => edit('删除 BPM', p => p.chart.timebase.tempos.splice(i, 1));
    row.append(tick, bpm, remove); $('tempos').append(row);
  });
}
function renderIssues() {
  $('issue-count').textContent = issues.length; $('issues-panel').replaceChildren();
  let repairs = [];
  try { repairs = planCameraRepair(chart()); } catch { /* Malformed timing requires manual correction. */ }
  if (repairs.length) {
    const repair = element('button', `顺延冲突运镜 · ${repairs.length} 个（可撤销）`, 'primary');
    repair.disabled = recording;
    repair.onclick = run(async () => {
      const plan = planCameraRepair(chart()), map = tempoMap(chart().timebase);
      const beyondAudio = plan.some(m => map.tickToSeconds(m.to + m.durationTicks) > audio.duration);
      const details = plan.map(m => `${m.id}：${m.from} → ${m.to} tick`).join('\n');
      if (!await confirm('顺延冲突运镜', `以下动作将按原顺序移到最早空闲时段，时长与目标参数保留。音符、音频与原始 Take 保留。\n${details}\n${beyondAudio ? '部分动作会延伸到音频结束之后，请复核。\n' : ''}整批修改可以 Ctrl+Z 撤销。`)) return;
      if (edit('顺延冲突运镜', p => { for (const m of plan) p.chart.actions.find(a => a.id === m.id).tick = m.to; }))
        setStatus(`已顺延 ${plan.length} 个运镜；请预览演出，Ctrl+Z 可撤销`);
    });
    $('issues-panel').append(repair);
  }
  if (!issues.length) $('issues-panel').append(element('p', '✓ 当前谱面通过协议与音频时间校验。自动预览仍需手机人工触控核验。', 'muted'));
  for (const issue of issues) {
    const button = element('button', `${issue.severity === 'error' ? '●' : '△'} ${issue.code} · ${issue.objectId || issue.path} — ${issue.message}`, `issue ${issue.severity}`);
    button.onclick = run(async () => { selected = issue.objectId || null; const obj = selectedObject(); if (obj) await seek(tempoMap(chart().timebase).tickToSeconds(obj.tick)); renderProperties(); });
    $('issues-panel').append(button);
  }
}
function renderTakes() {
  $('take-count').textContent = current().takes.length; $('takes-panel').replaceChildren();
  if (!current().takes.length) $('takes-panel').append(element('p', '平板录入后的原始样本会显示在这里。停止录制后可量化、导入或导出原始 Take。', 'muted'));
  for (const take of [...current().takes].reverse()) {
    const row = element('div', null, 'take-row');
    row.append(element('strong', `Take ${take.id.slice(0, 8)} · ${take.samples.length} 触点 · ${take.media?.name || take.chart?.settings?.title || '未知音源'}`));
    const summary = element('span', `${take.reason || take.status} · 待复核 ${take.samples.filter(s => s.quality === 'review').length}`, 'muted');
    const apply = element('button', '量化并导入'), raw = element('button', '导出原始');
    apply.disabled = recording;
    apply.onclick = run(async () => {
      const calibrationMs = Number($('take-calibration').value);
      if (!Number.isFinite(calibrationMs) || Math.abs(calibrationMs) > 1000) throw new Error('导入校准需在 ±1000 ms 内');
      const mismatch = take.media?.sha256 && take.media.sha256 !== current().audio?.sha256 ? '此 Take 的音源与当前工程不同，请确认确实要跨音乐导入。\n' : '';
      if (!await confirm('导入 Take', `${mismatch}使用当前 ${Number($('grid').value) ? '1/' + $('grid').value + ' 拍' : '自由 tick'} 网格、${calibrationMs} ms 校准与 ${tool === 'arrival' ? '到位' : '缩圈'} 类型生成音符。原始 Take 保留，整批导入可以撤销。`)) return;
      let added = [];
      edit('导入录制 Take', p => { added = importTake(p.chart, take, { division: Number($('grid').value), calibrationMs, motion: tool === 'arrival' ? 'arrival' : 'shrink' }); });
      selected = added[0] || null; refresh(); setStatus(`导入 ${added.length} 个音符；重叠与越界请检查校验列表`);
    });
    raw.onclick = run(async () => { const file = await api.exportTake(take); if (file) setStatus(`原始 Take 已保存：${file}`); });
    const controls = element('div', null, 'card-controls'); controls.append(apply, raw);
    row.append(summary, controls); $('takes-panel').append(row);
  }
}
function renderActions() {
  $('actions-panel').replaceChildren();
  for (const a of [...chart().actions].sort((a, b) => a.tick - b.tick)) {
    const row = element('div', null, 'action-row'), select = element('button', '选择');
    row.append(element('strong', `${a.id} · ${a.tick} → ${a.tick + a.durationTicks} tick`), element('span', `持续 ${a.durationTicks} tick · 旋转 ${a.rotation}° · 缩放 ×${a.scale}`, 'muted'));
    select.onclick = run(async () => { selected = a.id; await seek(tempoMap(chart().timebase).tickToSeconds(a.tick)); renderProperties(); });
    const controls = element('div', null, 'card-controls'); controls.append(select);
    row.append(controls); $('actions-panel').append(row);
  }
  if (!chart().actions.length) $('actions-panel').append(element('p', '在当前拍添加运镜，右侧编辑平移、旋转、缩放和缓动。相机动作不可重叠。', 'muted'));
}
function resizeTimeline() {
  const scroll = $('timeline-scroll'), content = $('timeline-content');
  const duration = Math.max(audio.duration, 10);
  timelinePps = Math.min(Number($('zoom').value), 10000000 / duration);
  timelineLayout = preview ? layoutTimeline(timelineDrag?.gesture?.original || chart(), timelinePps) : null;
  const width = Math.max(scroll.clientWidth, duration * timelinePps + TIMELINE.origin + 80);
  content.style.width = `${width}px`; content.style.height = `${Math.max(scroll.clientHeight, timelineLayout?.height || 190)}px`;
  // The canvas follows the viewport, not the full song/lane area: long or dense charts stay bounded.
  const dpr = Math.min(devicePixelRatio || 1, 2), viewWidth = scroll.clientWidth, viewHeight = scroll.clientHeight;
  timeline.style.width = `${viewWidth}px`; timeline.style.height = `${viewHeight}px`;
  timeline.width = Math.max(1, Math.round(viewWidth * dpr)); timeline.height = Math.max(1, Math.round(viewHeight * dpr));
}
function drawTimeline(song) {
  if (!timelineLayout) return;
  const scroll = $('timeline-scroll'), sx = scroll.scrollLeft, sy = scroll.scrollTop;
  const ctx = timeline.getContext('2d'), dpr = Math.min(devicePixelRatio || 1, 2), w = timeline.width / dpr, h = timeline.height / dpr;
  const { map, bars, sections } = timelineLayout, xAt = s => TIMELINE.origin + s * timelinePps;
  const duration = audio.duration || 10, firstTick = map.secondsToTick(sx / timelinePps), lastTick = map.secondsToTick((sx + w) / timelinePps);
  const tickStep = PPQ * Math.max(1, Math.ceil((lastTick - firstTick) / PPQ / 1000)), ticks = [];
  for (let t = Math.max(0, Math.floor(firstTick / tickStep) * tickStep); Number.isFinite(lastTick) && t <= lastTick; t += tickStep) ticks.push(t);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0); ctx.clearRect(0, 0, w, h);
  ctx.fillStyle = '#151f2c'; ctx.fillRect(0, 0, w, h); ctx.font = '11px Segoe UI'; ctx.textBaseline = 'middle'; ctx.textAlign = 'left';
  ctx.save(); ctx.beginPath(); ctx.rect(TIMELINE.origin, TIMELINE.ruler, w - TIMELINE.origin, h - TIMELINE.ruler); ctx.clip(); ctx.translate(-sx, -sy);
  for (const s of sections) {
    ctx.fillStyle = '#1a2635'; ctx.fillRect(sx, s.top, w, 22);
    for (let lane = 0; lane < s.lanes; lane++) {
      const y = s.top + 22 + lane * TIMELINE.laneHeight;
      if (y + TIMELINE.laneHeight < sy || y > sy + h) continue;
      ctx.fillStyle = lane % 2 ? '#192535' : '#162130'; ctx.fillRect(sx, y, w, TIMELINE.laneHeight);
    }
  }
  for (const t of ticks) {
    const x = xAt(map.tickToSeconds(t));
    ctx.strokeStyle = t % (4 * PPQ) === 0 ? '#435468' : '#26384b'; ctx.beginPath(); ctx.moveTo(x, sy); ctx.lineTo(x, sy + h); ctx.stroke();
  }
  if (waves) {
    ctx.strokeStyle = '#478f9b'; ctx.beginPath();
    waves.forEach((v, i) => { const x = xAt(i / waves.length * duration); if (x < sx || x > sx + w) return; ctx.moveTo(x, 58 - v * 19); ctx.lineTo(x, 58 + v * 19); }); ctx.stroke();
  }
  for (const b of bars) {
    const changing = timelineDrag?.gesture.id === b.id && timelineDrag.result;
    const left = changing ? timelineLayout.xAt(changing.start) : b.left;
    const right = changing ? Math.max(left + 22, timelineLayout.xAt(changing.end)) : b.right;
    if (right < sx + TIMELINE.origin || left > sx + w || b.bottom < sy + TIMELINE.ruler || b.top > sy + h) continue;
    const color = b.id === selected ? '#f4c783' : b.kind === 'camera' ? '#719af0' : b.motion === 'arrival' ? '#b89bff' : '#70e1df';
    ctx.fillStyle = color + '40'; ctx.fillRect(left, b.top, right - left, TIMELINE.barHeight);
    ctx.strokeStyle = color; ctx.lineWidth = b.id === selected ? 2 : 1; ctx.strokeRect(left + .5, b.top + .5, right - left - 1, TIMELINE.barHeight - 1);
    ctx.fillStyle = color; ctx.fillRect(left + 4, b.top + 5, 3, 12); ctx.fillRect(right - 7, b.top + 5, 3, 12);
    if (right - left > 55) {
      ctx.save(); ctx.beginPath(); ctx.rect(left + 12, b.top, right - left - 24, TIMELINE.barHeight); ctx.clip();
      ctx.fillStyle = '#edf3fa'; ctx.fillText(b.id, left + 13, b.top + 11); ctx.restore();
    }
  }
  ctx.restore();
  ctx.fillStyle = '#111c29'; ctx.fillRect(0, 0, TIMELINE.origin, h); ctx.fillStyle = '#9bb0c7';
  if (58 - sy > TIMELINE.ruler) ctx.fillText('波形', 12, 58 - sy);
  for (const s of sections) {
    if (s.top + 11 - sy > TIMELINE.ruler && s.top - sy < h) ctx.fillText(s.kind === 'note' ? '音符预读' : '运镜', 8, s.top + 11 - sy);
    for (let lane = 0; lane < s.lanes; lane++) {
      const y = s.top + 33 + lane * TIMELINE.laneHeight - sy;
      if (y > TIMELINE.ruler && y < h) ctx.fillText(`${s.kind === 'note' ? 'N' : 'C'} ${lane + 1}`, 16, y);
    }
  }
  ctx.fillStyle = '#253448'; ctx.fillRect(0, 0, w, TIMELINE.ruler); ctx.fillStyle = '#c0cfdf'; ctx.fillText('小节', 12, 16);
  for (const t of ticks) if (t % (4 * PPQ) === 0) { const x = xAt(map.tickToSeconds(t)) - sx; if (x >= TIMELINE.origin && x < w) ctx.fillText(String(t / PPQ / 4 + 1), x + 4, 17); }
  const x = xAt(song) - sx;
  if (x >= TIMELINE.origin && x <= w) {
    ctx.strokeStyle = '#f4c783'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke();
    ctx.fillStyle = '#f4c783'; ctx.beginPath(); ctx.moveTo(x - 7, 0); ctx.lineTo(x + 7, 0); ctx.lineTo(x + 7, 10); ctx.lineTo(x, 17); ctx.lineTo(x - 7, 10); ctx.closePath(); ctx.fill();
  }
}
async function syncHostClock() {
  let best = null;
  for (let i = 0; i < 8; i++) {
    const sent = performance.now() * 1000, pc = await api.clock(), received = performance.now() * 1000;
    const sample = { offset: pc - (sent + received) / 2, rtt: received - sent };
    if (!best || sample.rtt < best.rtt) best = sample;
  }
  hostOffsetUs = best.offset; hostClockUncertaintyUs = best.rtt / 2;
}
async function syncScene() {
  const owner = history, scene = chart(), text = JSON.stringify(scene);
  const chartHash = await sha256(new TextEncoder().encode(text));
  if (owner !== history || scene !== chart() || recording) return;
  return api.tabletCommand({ type: 'scene', chart: scene, chartHash,
    media: { name: current().audio?.name || '', sha256: current().audio?.sha256 || null, duration: audio.duration } });
}
async function publishTransport(discontinuity = false) {
  return api.tabletCommand({ type: 'transport', anchor: { ...audio.anchor(hostOffsetUs), desktopClockUncertaintyUs: hostClockUncertaintyUs }, discontinuity });
}
async function seek(seconds) {
  if (recording) await stopRecording();
  audio.seek(seconds); await publishTransport(true); renderProperties();
}
async function stopRecording() {
  const result = await api.tabletCommand({ type: 'record-end' });
  recording = false; activeTake = null; audio.pause(); await publishTransport(true);
  if (result.take) receiveTake(result.take);
  $('record').textContent = '● 开始录制 · 2 秒倒数'; $('record').classList.remove('live');
  refresh(); setStatus('录制结束；原始 Take 已保存，可量化后导入');
}
function receiveTake(take) {
  if (!take || pendingTakes.has(take.id)) return;
  const existing = current().takes.findIndex(t => t.id === take.id);
  if (existing >= 0) {
    if (current().takes[existing].samples.length >= take.samples.length) return;
    current().takes[existing] = clone(take);
    for (const snapshot of [...history.undoStack, ...history.redoStack]) {
      const i = snapshot.value.takes.findIndex(t => t.id === take.id);
      if (i >= 0) snapshot.value.takes[i] = clone(take);
    }
    history.revision++; dirty = true; renderTakes(); api.autosave(current()).catch(() => {}); return;
  }
  pendingTakes.add(take.id);
  // Captured raw data is retained outside undoable edits; undoing an edit must not delete a Take.
  const copy = clone(take);
  current().takes.push(copy);
  for (const snapshot of [...history.undoStack, ...history.redoStack]) if (!snapshot.value.takes.some(t => t.id === take.id)) snapshot.value.takes.push(clone(copy));
  history.revision++; dirty = true; renderTakes(); $('take-count').textContent = current().takes.length;
  pendingTakes.delete(take.id); api.autosave(current()).catch(e => setStatus(`自动保存失败：${e.message}`));
}
async function loadAudio(data) {
  const binary = atob(data.base64), bytes = Uint8Array.from(binary, x => x.charCodeAt(0));
  const hash = await sha256(bytes);
  if (data.sha256 && data.sha256 !== hash) throw new Error('工程音频 SHA-256 不一致');
  data.sha256 = hash;
  const buffer = await audio.load(bytes.buffer); waves = waveform(buffer);
  $('audio-info').textContent = `${data.name} · ${audio.duration.toFixed(2)} s · ${buffer.sampleRate} Hz`;
}
async function replaceProject(project) {
  if (timelineDrag) cancelTimelineDrag();
  if (recording) await stopRecording();
  audio.pause();
  if (project.audio) await loadAudio(project.audio);
  else { audio.buffer = null; waves = null; $('audio-info').textContent = '未导入音频'; }
  history = new History(project); selected = null; dirty = false;
  await syncHostClock(); refresh(); await publishTransport(true); lastAutosaveRevision = -1;
}
function selectTool(value) {
  tool = value;
  document.querySelectorAll('[data-tool]').forEach(e => e.classList.toggle('active', e.dataset.tool === value));
  stage.style.cursor = value === 'select' ? 'default' : 'crosshair';
}
document.querySelectorAll('[data-tool]').forEach(e => e.onclick = () => selectTool(e.dataset.tool));
document.querySelectorAll('[data-panel]').forEach(e => e.onclick = () => {
  showPanel(e.dataset.panel);
});
function stagePoint(event, pose = cameraAt(preview.chart, audio.now())) {
  const r = stage.getBoundingClientRect();
  return unprojectPoint(normalizedToView((event.clientX - r.left) / r.width, 1 - (event.clientY - r.top) / r.height), pose);
}
stage.onpointerdown = run(async event => {
  if (recording || audio.playing || !preview) return;
  const point = stagePoint(event), map = tempoMap(chart().timebase);
  if (tool !== 'select') {
    let id;
    edit('放置音符', p => { id = addNote(p.chart, { tick: Math.max(PPQ, quantize(map.secondsToTick(audio.now()), Number($('grid').value))), point, motion: tool }).id; });
    selected = id; renderProperties(); return;
  }
  const candidates = chart().notes.filter(n => Math.hypot(n.target.x - point.x, n.target.y - point.y) <= n.radius + 2)
    .sort((a, b) => Math.abs(map.tickToSeconds(a.tick) - audio.now()) - Math.abs(map.tickToSeconds(b.tick) - audio.now()));
  const currentNote = chart().notes.find(n => n.id === selected), path = chart().paths.find(p => p.id === currentNote?.pathId);
  const isStart = path && Math.hypot(path.start.x - point.x, path.start.y - point.y) < 3;
  const note = isStart ? currentNote : candidates[0]; selected = note?.id || null; renderProperties();
  if (note) { drag = { id: note.id, start: isStart, point, pose: cameraAt(chart(), audio.now()), chart: clone(chart()) }; stage.setPointerCapture(event.pointerId); }
});
stage.onpointermove = event => {
  if (!drag) return;
  const point = stagePoint(event, drag.pose); drag.point = point;
  const note = drag.chart.notes.find(n => n.id === drag.id);
  if (drag.start) drag.chart.paths.find(p => p.id === note.pathId).start = point;
  else moveTarget(drag.chart, note, point);
  preview = compilePreview(drag.chart);
};
stage.onpointerup = () => {
  if (!drag) return;
  const d = drag; drag = null;
  edit(d.start ? '拖动路径起点' : '拖动接收目标', p => {
    const n = p.chart.notes.find(n => n.id === d.id);
    if (d.start) p.chart.paths.find(x => x.id === n.pathId).start = d.point; else moveTarget(p.chart, n, d.point);
  });
  refresh();
};
stage.onpointercancel = () => { drag = null; refresh(); };
function timelinePoint(event) {
  const r = timeline.getBoundingClientRect(), scroll = $('timeline-scroll');
  return { x: event.clientX - r.left + scroll.scrollLeft, y: event.clientY - r.top + scroll.scrollTop,
    localX: event.clientX - r.left, localY: event.clientY - r.top };
}
function timelineHit(event) {
  const p = timelinePoint(event);
  if (p.localX < TIMELINE.origin || !timelineLayout) return null;
  return p.localY < TIMELINE.ruler ? { mode: 'scrub' } : hitTimeline(timelineLayout, p.x, p.y, { selected, song: audio.now() });
}
function updateTimelineDrag() {
  const d = timelineDrag;
  if (!d?.pending) return;
  d.pending = false;
  const p = timelinePoint(d.pointer), seconds = (p.x - TIMELINE.origin) / timelinePps;
  if (d.gesture.mode === 'scrub') {
    audio.seek(scrubSeconds(timelineLayout, p.x, audio.duration));
    setStatus(`定位 ${audio.now().toFixed(3)} 秒 · 松开结束；Esc 返回拖动前位置`);
  } else {
    d.result = moveTimelineGesture(d.gesture, seconds); preview = compilePreview(d.result.chart);
    const part = d.gesture.mode === 'move' ? '移动' : d.gesture.mode === 'start' ? '调整左端' : '调整右端';
    setStatus(`${part} ${d.gesture.id} · ${d.result.start} → ${d.result.end} tick · 松开提交，Esc 取消`);
  }
}
function cancelTimelineDrag() {
  const d = timelineDrag; if (!d) return;
  timelineDrag = null;
  if (d.gesture.mode === 'scrub') { audio.seek(d.initialSong); publishTransport(true).catch(() => {}); }
  try { if (timeline.hasPointerCapture(d.pointerId)) timeline.releasePointerCapture(d.pointerId); } catch {}
  timeline.style.cursor = 'default'; refresh(); setStatus('已取消时间轴拖动');
}
timeline.onpointerdown = run(async event => {
  if (event.button !== 0 || recording || !preview || timelineDrag) return;
  const hit = timelineHit(event); if (!hit) return;
  event.preventDefault(); timeline.focus({ preventScroll: true });
  const initialSong = audio.now(); audio.pause();
  const p = timelinePoint(event), seconds = (p.x - TIMELINE.origin) / timelinePps;
  const gesture = beginTimelineGesture(chart(), hit, seconds, Number($('grid').value));
  if (hit.bar) { selected = hit.bar.id; renderProperties(); }
  timelineDrag = { gesture, initialSong, pointerId: event.pointerId, pointer: event, initialX: event.clientX,
    movedPointer: false, pending: hit.mode === 'scrub', result: null };
  timeline.setPointerCapture(event.pointerId); updateTimelineDrag();
  timeline.style.cursor = hit.mode === 'move' ? 'grabbing' : hit.mode === 'scrub' ? 'col-resize' : 'ew-resize';
  await publishTransport(true);
});
timeline.onpointermove = run(async event => {
  if (timelineDrag) {
    if (event.pointerId === timelineDrag.pointerId) {
      timelineDrag.pointer = event; timelineDrag.pending = true;
      if (Math.abs(event.clientX - timelineDrag.initialX) > 3) timelineDrag.movedPointer = true;
    }
    return;
  }
  const hit = !recording && preview ? timelineHit(event) : null;
  timeline.style.cursor = hit?.mode === 'move' ? 'grab' : hit?.mode === 'scrub' ? 'col-resize' : hit ? 'ew-resize' : 'default';
});
timeline.onpointerup = run(async event => {
  if (!timelineDrag || event.pointerId !== timelineDrag.pointerId) return;
  timelineDrag.pointer = event; timelineDrag.pending = true;
  try { updateTimelineDrag(); } catch (e) { cancelTimelineDrag(); throw e; }
  const d = timelineDrag; timelineDrag = null; timeline.style.cursor = 'default';
  try {
    if (d.gesture.mode === 'scrub') { await publishTransport(true); renderProperties(); setStatus(`已定位 ${audio.now().toFixed(3)} 秒`); }
    else if (d.result?.changed) {
      edit(d.gesture.mode === 'move' ? '移动时间范围' : '拉伸时间范围', p => commitTimelineGesture(p.chart, d.gesture, d.result));
      setStatus(`已更新 ${d.gesture.id} · ${d.result.start} → ${d.result.end} tick；Ctrl+Z 可撤销`);
    }
  } finally { refresh(); }
});
timeline.onpointercancel = cancelTimelineDrag;
timeline.onlostpointercapture = cancelTimelineDrag;
timeline.onpointerleave = () => { if (!timelineDrag) timeline.style.cursor = 'default'; };
timeline.onkeydown = run(async event => {
  if (!['ArrowLeft', 'ArrowRight'].includes(event.key) || recording || !timelineLayout || timelineDrag) return;
  event.preventDefault(); const map = timelineLayout.map, delta = gridStep(Number($('grid').value)) * (event.key === 'ArrowLeft' ? -1 : 1);
  await seek(map.tickToSeconds(Math.max(0, map.secondsToTick(audio.now()) + delta)));
});
$('zoom').oninput = () => { if (timelineDrag) cancelTimelineDrag(); resizeTimeline(); };
for (const id of ['loop', 'loop-a', 'loop-b']) $(id).onchange = () => {
  const a = Number($('loop-a').value), b = Number($('loop-b').value), enabled = $('loop').checked;
  if (a < 0 || b <= a || !Number.isFinite(a) || !Number.isFinite(b)) { setStatus('循环范围需要 0≤A<B'); return; }
  edit('修改 A/B 循环', p => p.loop = { enabled, a, b });
};
$('play').onclick = run(async () => {
  if (timelineDrag) cancelTimelineDrag();
  if (recording) { await stopRecording(); return; }
  if (!preview) throw new Error('请先修复下方“校验”中的错误，再播放预览');
  if (audio.playing) { audio.pause(); await publishTransport(true); }
  else { await syncHostClock(); await audio.play(); $('layout').checked = false; await publishTransport(true); }
});
$('stop').onclick = run(async () => seek(0));
$('undo').onclick = () => { if (timelineDrag) cancelTimelineDrag(); if (!recording && history.undo()) { dirty = true; selected = null; refresh(); } };
$('redo').onclick = () => { if (timelineDrag) cancelTimelineDrag(); if (!recording && history.redo()) { dirty = true; selected = null; refresh(); } };
$('delete').onclick = () => {
  if (!selected) return;
  edit('删除对象', p => { const n = p.chart.notes.find(x => x.id === selected); p.chart.notes = p.chart.notes.filter(x => x.id !== selected);
    if (n?.pathId && !p.chart.notes.some(x => x.pathId === n.pathId)) p.chart.paths = p.chart.paths.filter(x => x.id !== n.pathId);
    p.chart.actions = p.chart.actions.filter(x => x.id !== selected); updateTouches(p.chart); });
  selected = null; renderProperties();
};
$('copy').onclick = () => {
  const n = chart().notes.find(x => x.id === selected); if (!n) { setStatus('复制功能用于选中的音符'); return; }
  let id;
  edit('复制音符到下一拍', p => { const path = p.chart.paths.find(x => x.id === n.pathId);
    const copy = addNote(p.chart, { tick: n.tick + PPQ, preRoll: n.tick - n.spawnTick, point: { x: n.target.x + 8, y: n.target.y }, motion: n.motion, radius: n.radius,
      start: path ? { x: path.start.x + 8, y: path.start.y } : undefined }); id = copy.id; });
  selected = id; renderProperties();
};
$('camera-add').onclick = run(async () => {
  const tick = Math.max(0, quantize(tempoMap(chart().timebase).secondsToTick(audio.now()), Number($('grid').value)));
  const pose = cameraAt(chart(), audio.now()); let id;
  edit('添加相机动作', p => { id = addCamera(p.chart, { tick, pose }).id; });
  selected = id; renderProperties();
});
$('tempo-add').onclick = () => {
  const tick = Math.max(1, quantize(tempoMap(chart().timebase).secondsToTick(audio.now()), Number($('grid').value)));
  if (chart().timebase.tempos.some(t => t.tick === tick)) { setStatus('此 tick 已有 BPM 段'); return; }
  edit('添加 BPM 段', p => { p.chart.timebase.tempos.push({ tick, bpm: 120 }); p.chart.timebase.tempos.sort((a, b) => a.tick - b.tick); });
};
$('new').onclick = run(async () => {
  if (dirty && !await confirm('新建工程', '当前工程有未保存的修改。继续前将保留自动恢复草稿。')) return;
  await api.autosave(current()); await replaceProject(createProject()); dirty = true;
});
$('open').onclick = run(async () => {
  if (dirty && !await confirm('打开工程', '将替换当前工程。当前编辑会先保存到自动恢复草稿。')) return;
  await api.autosave(current()); const file = await api.open(); if (file) await replaceProject(file.project || createProject(file.chart));
});
$('save').onclick = run(async () => {
  if (saving) return; saving = true;
  try { const file = await api.save(current(), false); if (file) { dirty = false; $('save-state').textContent = file.split(/[\\/]/).at(-1); setStatus(`工程已保存：${file}`); } }
  finally { saving = false; }
});
$('audio').onclick = run(async () => {
  if (recording) await stopRecording(); const data = await api.importAudio(); if (!data) return;
  await loadAudio(data); edit('导入音频', p => p.audio = data); await publishTransport(true);
});
$('export').onclick = run(async () => {
  if (!audio.buffer) throw new Error('导出前请导入音频，以校验音符尾界');
  const text = exportChart(chart(), { duration: audio.duration || Infinity });
  const warnings = issues.filter(x => x.severity === 'warning');
  if (warnings.length && !await confirm('导出前检查', `还有 ${warnings.length} 条警告，请先检查可读性与触区。仍然导出当前谱面？`)) return;
  const file = await api.exportChart(text); if (file) setStatus(`游戏谱面已导出：${file}`);
});
$('tablet').onclick = run(async () => {
  if (tabletInfo?.running) {
    if (recording) await stopRecording(); await api.tabletStop(); tabletInfo = null; $('pairing').hidden = true; $('tablet').textContent = '开启平板连接'; return;
  }
  tabletInfo = await api.tabletStart(); $('pairing').hidden = false; $('tablet').textContent = '关闭平板连接'; $('qr').src = tabletInfo.qr;
  $('address').replaceChildren();
  for (const url of tabletInfo.urls) { const o = element('option', new URL(url).host); o.value = url; $('address').append(o); }
  $('pair-url').value = tabletInfo.urls[0]; await syncHostClock();
  await syncScene(); await publishTransport(true); setStatus('平板连接已开启；扫码或在平板浏览器输入地址');
});
$('address').onchange = () => { $('pair-url').value = $('address').value; setStatus('二维码对应首次显示地址；选择其他网卡时请复制完整地址到平板'); };
$('record').onclick = run(async () => {
  if (timelineDrag) cancelTimelineDrag();
  if (recording) { await stopRecording(); return; }
  if (!tabletInfo?.running) throw new Error('请先开启平板连接，并等待平板完成同步');
  if (issues.some(x => x.severity === 'error' && x.code !== 'E_EMPTY')) throw new Error('请先修复谱面校验错误');
  if ($('loop').checked) throw new Error('录制前请关闭 A/B 循环');
  if (current().takes.length >= 100) throw new Error('当前工程已有 100 个 Take，请新建工程继续录制');
  await syncScene(); await syncHostClock(); audio.pause(); await audio.play(2); $('layout').checked = false;
  await publishTransport(true);
  const result = await api.tabletCommand({ type: 'record-begin' });
  activeTake = result.snapshot.takeId; recording = true; $('record').textContent = '■ 停止录制'; $('record').classList.add('live');
  refresh(); setStatus('录制已安排；两秒后听电脑音乐，在平板画布落指');
});
$('recover').onclick = run(async () => {
  const saved = recoveryOffer || await api.recover();
  if (!saved.project && !saved.takes?.length) throw new Error('尚无恢复草稿');
  if (!await confirm('恢复草稿', '恢复上次自动保存工程，并找回已经落盘的原始 Take。当前工程先自动保存。')) return;
  await api.autosave(current()); await replaceProject(saved.project || createProject());
  for (const t of saved.takes || []) receiveTake(t);
  recoveryOffer = null; dirty = true; refresh();
});
api.onBeforeClose?.(async () => { if (recording) await stopRecording(); if (history && dirty) await api.autosave(current()); });
api.onTablet(event => {
  if (event.type === 'devices') {
    if (tabletInfo) tabletInfo.devices = event.info.devices;
    $('device-state').textContent = event.info.devices.map(d => `${d.model?.sampleCount >= 3 ? '已同步' : '同步中'} · RTT ${((d.model?.rttUs || 0) / 1000).toFixed(1)} ms · 峰值 ${d.peakTouches} 指`).join('\n') || '等待平板连接';
  } else if (event.type === 'samples') $('record-state').textContent = `● 录制中 · ${event.count} 个原始触点已保存`;
  else if (event.type === 'take-updated') receiveTake(event.take);
  else if (event.type === 'take-ended') {
    receiveTake(event.take);
    if (event.take.id === activeTake) { recording = false; activeTake = null; audio.pause(); $('record').textContent = '● 开始录制 · 2 秒倒数'; $('record').classList.remove('live'); refresh(); }
  }
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && timelineDrag) { event.preventDefault(); cancelTimelineDrag(); return; }
  if (['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName) || $('confirm').open) return;
  const cmd = event.ctrlKey || event.metaKey;
  const action = cmd && event.key.toLowerCase() === 's' ? 'save' : cmd && event.key.toLowerCase() === 'z' ? event.shiftKey ? 'redo' : 'undo' :
    cmd && event.key.toLowerCase() === 'y' ? 'redo' : event.code === 'Space' ? 'play' : event.key === 'Delete' ? 'delete' : event.key.toLowerCase() === 'c' ? 'camera-add' : null;
  if (action) { event.preventDefault(); if (timelineDrag) cancelTimelineDrag(); $(action).click(); }
  else if (event.key === '1') selectTool('shrink'); else if (event.key === '2') selectTool('arrival'); else if (event.key.toLowerCase() === 'v') selectTool('select');
});
new ResizeObserver(() => { fitCanvas(stage); resizeTimeline(); if (history && !preview) drawValidationError(); }).observe(stage.parentElement);
new ResizeObserver(resizeTimeline).observe($('timeline-scroll'));
setInterval(run(async () => {
  if (!history || !dirty || history.revision === lastAutosaveRevision) return;
  await api.autosave(current()); lastAutosaveRevision = history.revision;
}), 15000);
window.addEventListener('blur', () => { if (history && dirty) api.autosave(current()).catch(() => {}); });
document.addEventListener('visibilitychange', run(async () => { if (document.hidden && audio.playing) { if (recording) await stopRecording(); else { audio.pause(); await publishTransport(true); } } }));
function frame(now) {
  if (preview && history) {
    if (timelineDrag) {
      const scroll = $('timeline-scroll'), r = scroll.getBoundingClientRect(), x = timelineDrag.pointer.clientX;
      const delta = !timelineDrag.movedPointer ? 0 : x < r.left + TIMELINE.origin + 18 ? -14 : x > r.right - 24 ? 14 : 0;
      const before = scroll.scrollLeft;
      if (delta) scroll.scrollLeft += delta;
      if (before !== scroll.scrollLeft) timelineDrag.pending = true;
      try { updateTimelineDrag(); } catch (e) { cancelTimelineDrag(); setStatus(e.message); }
    }
    let song = audio.now();
    if (audio.playing && song >= audio.duration - .01) { if (recording) run(stopRecording)(); else { audio.pause(); publishTransport(true).catch(() => {}); } }
    const a = Number($('loop-a').value), b = Number($('loop-b').value);
    if (audio.playing && !recording && $('loop').checked && b > a && song >= b) run(async () => { audio.seek(a); await audio.play(); await publishTransport(true); })();
    drawStage(stage, preview, song, { selected, layout: $('layout').checked && !audio.playing, clean: $('clean').checked });
    drawTimeline(song);
    if (now - lastUI > 100) {
      lastUI = now; const map = preview.map, tick = Math.max(0, map.secondsToTick(song)), beat = Math.floor(tick / PPQ);
      $('time').textContent = `${String(Math.floor(song / 60)).padStart(2, '0')}:${(song % 60).toFixed(3).padStart(6, '0')}`;
      $('beat').textContent = `${Math.floor(beat / 4) + 1} : ${beat % 4 + 1} : ${Math.round(tick % PPQ).toString().padStart(3, '0')}`;
      $('play').textContent = audio.playing ? 'Ⅱ' : '▶'; $('stage-mode').textContent = recording ? 'RECORDING' : audio.playing ? 'PREVIEW' : $('layout').checked ? 'LAYOUT' : 'FRAME';
      if (audio.playing && $('metronome').checked && beat !== lastBeat) audio.tickClick(); lastBeat = beat;
    }
  }
  requestAnimationFrame(frame);
}
try {
  recoveryOffer = await api.recover();
  const sampleChart = await (await fetch('../../assets/sample-chart.json')).json();
  const bytes = new Uint8Array(await (await fetch('../../assets/sample-audio.wav')).arrayBuffer());
  let binary = ''; for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
  await replaceProject(createProject(sampleChart, { name: '原创节拍演示.wav', base64: btoa(binary) }));
  audio.seek(4); await publishTransport(true);
  if (recoveryOffer.project || recoveryOffer.takes?.length) setStatus('发现上次草稿；点击左侧“恢复自动草稿”可找回');
  fitCanvas(stage);
  drawStage(stage, preview, audio.now(), { layout: true });
  drawTimeline(audio.now());
  let layoutEvidence, takeLayoutEvidence;
  if (new URL(location.href).searchParams.get('verify') === '1') {
    showPanel('actions');
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    const { verifyLayout } = await import('./layout-verification.mjs');
    layoutEvidence = verifyLayout(document, { innerHeight, devicePixelRatio });
    // Synthetic card fixtures exercise wrapping; no captured/user Take is modified or persisted.
    const originalTakes = current().takes;
    current().takes = [{ id: 'layout-fixture', media: { name: '中文路径与很长的录入音乐名称-'.repeat(12) + '.wav' }, reason: 'interrupted', samples: [] }];
    renderTakes(); showPanel('takes');
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    takeLayoutEvidence = verifyLayout(document, { innerHeight, devicePixelRatio });
    current().takes = originalTakes; renderTakes();
    showPanel('issues');
  }
  await api.reportHealth({ status: 'ready', schemaVersion: chart().schemaVersion, noteCount: chart().notes.length,
    audioDuration: audio.duration, sampleRate: audio.buffer.sampleRate, stageWidth: stage.width, stageHeight: stage.height,
    validationErrors: issues.filter(i => i.severity === 'error').length, nativeBridge: !!window.editorAPI,
    ...(layoutEvidence ? { layout: layoutEvidence, takeLayout: takeLayoutEvidence } : {}), timestamp: new Date().toISOString() });
  requestAnimationFrame(frame);
} catch (e) {
  setStatus(`启动失败：${e.message}`);
  await api.reportHealth({ status: 'failed', error: e.message, timestamp: new Date().toISOString() }).catch(() => {});
}
