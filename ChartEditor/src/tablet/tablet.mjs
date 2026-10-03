import { cameraAt, normalizedToView, unprojectPoint } from '/core/chart.mjs';
import { compilePreview, drawStage, fitCanvas } from '/ui/render.mjs';
const $ = id => document.getElementById(id), stage = $('stage');
const token = location.hash.slice(1); history.replaceState(null, '', location.pathname);
let socket, snapshot = null, model = null, preview = null, generation = -1, seq = 0, count = 0, frameVersion = 0;
const pending = new Map(), poses = [], active = new Set(), flashes = [];
function send(m) { if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(m)); }
function connect() {
  if (!token) { $('connection').textContent = '请重新扫描电脑上的配对码'; return; }
  socket = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/touch?token=${encodeURIComponent(token)}`);
  socket.onopen = () => { $('connection').textContent = '已连接 · 同步中'; send({ type: 'Hello', protocolVersion: 'ring-touch-1', deviceName: navigator.userAgent.slice(0, 80) }); };
  socket.onmessage = ({ data }) => {
    const m = JSON.parse(data);
    if (m.type === 'ClockPing') {
      const clientReceiveUs = performance.now() * 1000;
      send({ type: 'ClockPong', seq: m.seq, clientReceiveUs, clientSendUs: performance.now() * 1000 });
    } else if (m.type === 'ClockModel') model = m.model;
    else if (m.type === 'SongSnapshot') {
      if (m.canvasVersion !== snapshot?.canvasVersion && m.chart) preview = compilePreview(m.chart);
      if (m.generation !== generation || m.takeId !== snapshot?.takeId) {
        pending.clear(); seq = 0; count = 0; poses.length = 0; active.clear(); generation = m.generation;
      }
      snapshot = m;
      document.body.classList.toggle('live', m.recording);
      $('connection').textContent = m.recording ? '● 录制中' : '已连接';
    } else if (m.type === 'Ack') {
      if (m.takeId === snapshot?.takeId && m.generation === generation) for (const key of pending.keys()) if (key <= m.contiguousSeq) pending.delete(key);
    } else if (m.type === 'Error') { $('overlay').textContent = m.message; }
  };
  socket.onclose = () => {
    snapshot = null; model = null; pending.clear(); poses.length = 0; active.clear();
    document.body.classList.remove('live'); $('connection').textContent = '断线 · 本轮已停止';
    $('overlay').textContent = '正在重连；连接恢复后请在电脑开始新的 Take';
    setTimeout(connect, 2000);
  };
  socket.onerror = () => {};
}
function songAt(clientUs) {
  if (!snapshot || !model) return 0;
  const t = snapshot.transport;
  return (t.songUs + (t.state === 'playing' ? Math.max(0, model.a * clientUs + model.b - t.pcMonoUs) : 0)) / 1e6;
}
function resize() {
  const main = stage.parentElement, w = Math.min(main.clientWidth, main.clientHeight * 16 / 9);
  stage.style.width = `${w}px`; stage.style.height = `${w * 9 / 16}px`; fitCanvas(stage);
  poses.length = 0; active.clear();
}
new ResizeObserver(resize).observe(stage.parentElement);
stage.addEventListener('pointerdown', event => {
  event.preventDefault(); active.add(event.pointerId); stage.setPointerCapture(event.pointerId);
  if (!snapshot?.recording || !model || model.sampleCount < 3 || pending.size >= 128) return;
  let stamp = event.timeStamp;
  if (stamp > 1e12) stamp -= performance.timeOrigin;
  if (!Number.isFinite(stamp) || Math.abs(stamp - performance.now()) > 5000) { $('overlay').textContent = '输入时间戳异常，本次未录入'; return; }
  const displayMs = Math.max(0, Math.min(100, Number($('latency').value) || 0));
  const targetUs = (stamp - displayMs) * 1000;
  let frame = null;
  for (let i = poses.length - 1; i >= 0; i--) if (poses[i].clientUs <= targetUs) { frame = poses[i]; break; }
  if (!frame || targetUs - frame.clientUs > 500000) { $('overlay').textContent = '缺少对应历史画面，本次未录入'; return; }
  const rect = stage.getBoundingClientRect(), x = (event.clientX - rect.left) / rect.width, y = 1 - (event.clientY - rect.top) / rect.height;
  if (x < 0 || x > 1 || y < 0 || y > 1) return;
  const sample = { sampleIndex: 0, touchId: event.pointerId, phase: 'began', startMonoUs: stamp * 1000,
    x, y, rawX: event.clientX, rawY: event.clientY, viewport: { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
    canvasVersion: snapshot.canvasVersion, transformVersion: frame.version, displaySongUs: frame.song * 1e6,
    pose: frame.pose, clockModelId: model.id, displayLatencyMs: displayMs, pointerType: event.pointerType };
  const batch = { type: 'TouchBatch', sessionId: snapshot.sessionId, takeId: snapshot.takeId, generation, seq: seq++, activeTouches: active.size, samples: [sample] };
  pending.set(batch.seq, batch); send(batch); count++;
  flashes.push({ time: performance.now(), point: unprojectPoint(normalizedToView(x, y), frame.pose) });
  $('count').textContent = `${count} 触点 · ${active.size} 指`;
});
for (const type of ['pointerup', 'pointercancel', 'lostpointercapture']) stage.addEventListener(type, e => active.delete(e.pointerId));
document.addEventListener('visibilitychange', () => { if (document.hidden) { socket?.close(); active.clear(); } });
$('fullscreen').onclick = () => { if (document.fullscreenElement) document.exitFullscreen?.(); else document.documentElement.requestFullscreen?.().catch(() => { $('overlay').textContent = '浏览器不支持全屏，可横屏使用'; }); };
setInterval(() => { for (const batch of pending.values()) send(batch); }, 1000);
let lastStatus = 0;
function frame(now) {
  if (preview && model && snapshot) {
    const clientUs = performance.now() * 1000, song = songAt(clientUs), pose = cameraAt(preview.chart, song);
    drawStage(stage, preview, song, { framePose: pose, flashes });
    poses.push({ clientUs, song, pose, version: frameVersion++ });
    while (poses.length > 300 || poses[0]?.clientUs < clientUs - 2e6) poses.shift();
    while (flashes.length && now - flashes[0].time > 450) flashes.shift();
    if (now - lastStatus > 100) {
      lastStatus = now; $('song').textContent = `${song.toFixed(3)} s`;
      $('quality').textContent = `RTT ${(model.rttUs / 1000).toFixed(1)} ms · 不确定度 ±${(model.uncertaintyUs / 1000).toFixed(1)} ms`;
      $('overlay').textContent = snapshot.recording ? (model.a * clientUs + model.b < snapshot.transport.pcMonoUs ? '倒数中，请准备' : '点击画布录入 · 多指可同时落下') : '请在电脑上开启录制';
    }
  }
  requestAnimationFrame(frame);
}
connect(); resize(); requestAnimationFrame(frame);
