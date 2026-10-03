import { tempoMap, cameraAt, projectPoint, WIDTH, clamp } from '../core/chart.mjs';

export function compilePreview(chart) {
  const map = tempoMap(chart.timebase), paths = new Map(chart.paths.map(p => [p.id, p]));
  const notes = chart.notes.map(n => ({ ...n, spawn: map.tickToSeconds(n.spawnTick), hit: map.tickToSeconds(n.tick), path: paths.get(n.pathId) }));
  notes.sort((a, b) => a.spawn - b.spawn);
  return { chart, map, notes, maxPreRoll: Math.max(2, ...notes.map(n => n.hit - n.spawn)) };
}
export function fitCanvas(canvas) {
  const rect = canvas.getBoundingClientRect(), dpr = Math.min(devicePixelRatio || 1, 2);
  const width = Math.max(1, Math.round(rect.width * dpr)), height = Math.max(1, Math.round(rect.height * dpr));
  if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
  return { width, height, rect, dpr };
}
export function drawStage(canvas, preview, song, { selected = null, layout = false, clean = false, flashes = [], framePose } = {}) {
  const ctx = canvas.getContext('2d'), w = canvas.width, h = canvas.height;
  ctx.clearRect(0, 0, w, h);
  ctx.fillStyle = '#111926'; ctx.fillRect(0, 0, w, h);
  const pose = framePose || cameraAt(preview.chart, song), scale = h / 100;
  const pixel = p => { const v = projectPoint(p, pose); return { x: w / 2 + v.x * scale, y: h / 2 - v.y * scale }; };
  if (!clean) {
    ctx.strokeStyle = '#203043'; ctx.lineWidth = 1;
    for (let x = -80; x <= 80; x += 10) { const a = pixel({ x, y: -50 }), b = pixel({ x, y: 50 }); ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke(); }
    for (let y = -50; y <= 50; y += 10) { const a = pixel({ x: -WIDTH / 2, y }), b = pixel({ x: WIDTH / 2, y }); ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke(); }
    ctx.strokeStyle = '#456273'; ctx.setLineDash([7, 6]); ctx.strokeRect(scale * 4, scale * 4, w - scale * 8, h - scale * 8); ctx.setLineDash([]);
  }
  const circle = (p, r, color, fill = false, width = 2) => {
    const v = pixel(p); ctx.beginPath(); ctx.arc(v.x, v.y, Math.max(.01, r * scale * pose.scale), 0, Math.PI * 2);
    ctx.strokeStyle = color; ctx.lineWidth = width * Math.min(devicePixelRatio || 1, 2);
    if (fill) { ctx.fillStyle = color + '22'; ctx.fill(); } ctx.stroke();
  };
  // Sorted spawn times allow stopping after the visible interval. No per-frame JSON compilation.
  for (const n of preview.notes) {
    if (!layout && n.spawn > song) break;
    if (layout ? Math.abs(n.hit - song) > 2 && n.id !== selected : song > n.hit + .1) continue;
    const u = clamp((song - n.spawn) / (n.hit - n.spawn), 0, 1), color = n.motion === 'shrink' ? '#70e1df' : '#b89bff';
    if (n.motion === 'arrival' && n.path && !clean) {
      const a = pixel(n.path.start), b = pixel(n.target);
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.strokeStyle = '#887ca766'; ctx.lineWidth = 2;
      ctx.setLineDash([4, 5]); ctx.stroke(); ctx.setLineDash([]);
      if (n.id === selected) circle(n.path.start, 1.6, '#f4c783', true);
    }
    circle(n.target, n.radius, color, true);
    if (!layout || n.id === selected) {
      if (n.motion === 'shrink') circle(n.target, n.radius * (3 - 2 * u), color + 'aa');
      else if (n.path) circle({ x: n.path.start.x + (n.target.x - n.path.start.x) * u, y: n.path.start.y + (n.target.y - n.path.start.y) * u }, n.radius, '#efdeff');
    }
    if (n.id === selected) circle(n.target, n.radius + 1.8, '#f4c783', false, 1);
    const p = pixel(n.target);
    ctx.fillStyle = '#eff8ff'; ctx.font = `${Math.max(11, 2.3 * scale)}px system-ui`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText(n.id, p.x, p.y);
  }
  for (const f of flashes) {
    const age = performance.now() - f.time;
    if (age > 450) continue;
    ctx.globalAlpha = 1 - age / 450;
    circle(f.point, 2 + age / 35, '#f4c783'); ctx.globalAlpha = 1;
  }
  return pose;
}

export function waveform(audio, bins = 1200) {
  const data = audio.getChannelData(0), step = Math.max(1, Math.floor(data.length / bins));
  const result = new Float32Array(bins);
  for (let i = 0; i < bins; i++) {
    let max = 0;
    for (let j = i * step; j < Math.min(data.length, (i + 1) * step); j += Math.max(1, Math.floor(step / 80))) max = Math.max(max, Math.abs(data[j]));
    result[i] = max;
  }
  return result;
}
