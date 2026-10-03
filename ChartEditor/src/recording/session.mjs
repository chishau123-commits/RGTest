import { randomUUID } from 'node:crypto';
import { clone, cameraAt, unprojectPoint, normalizedToView } from '../core/chart.mjs';

export const PROTOCOL = 'ring-touch-1';
export class ClockSync {
  constructor() { this.samples = []; this.model = null; }
  add({ pcSendUs, clientReceiveUs, clientSendUs, pcReceiveUs }) {
    const values = [pcSendUs, clientReceiveUs, clientSendUs, pcReceiveUs];
    if (!values.every(Number.isFinite) || clientSendUs < clientReceiveUs || pcReceiveUs < pcSendUs) throw new Error('Invalid clock sample');
    const rttUs = (pcReceiveUs - pcSendUs) - (clientSendUs - clientReceiveUs);
    if (rttUs < 0 || rttUs > 500000) throw new Error('Clock RTT outside accepted range');
    const clientUs = (clientReceiveUs + clientSendUs) / 2, pcUs = (pcSendUs + pcReceiveUs) / 2;
    this.samples.push({ clientUs, pcUs, rttUs });
    if (this.samples.length > 64) this.samples.shift();
    const best = [...this.samples].sort((a, b) => a.rttUs - b.rttUs).slice(0, 8);
    let a = 1;
    const span = Math.max(...best.map(x => x.clientUs)) - Math.min(...best.map(x => x.clientUs));
    if (best.length >= 4 && span > 20e6) {
      const cx = best.reduce((v, s) => v + s.clientUs, 0) / best.length;
      const cy = best.reduce((v, s) => v + s.pcUs, 0) / best.length;
      const numerator = best.reduce((v, s) => v + (s.clientUs - cx) * (s.pcUs - cy), 0);
      const denominator = best.reduce((v, s) => v + (s.clientUs - cx) ** 2, 0);
      a = Math.max(.999, Math.min(1.001, numerator / denominator));
    }
    const s = best[0], b = s.pcUs - a * s.clientUs;
    const residual = Math.max(...best.map(x => Math.abs(a * x.clientUs + b - x.pcUs)));
    this.model = { id: randomUUID(), a, b, rttUs: s.rttUs, uncertaintyUs: s.rttUs / 2 + residual,
      measuredPcUs: pcReceiveUs, sampleCount: this.samples.length };
    return this.model;
  }
}

export class RecordingSession {
  constructor(nowUs = () => performance.now() * 1000) {
    this.nowUs = nowUs; this.sessionId = randomUUID(); this.generation = 0; this.canvasVersion = 0;
    this.scene = null; this.transport = { state: 'paused', songUs: 0, pcMonoUs: nowUs() };
    this.take = null; this.history = []; this.retired = new Map();
  }
  setScene(chart, metadata = {}) { this.abort('谱面更新'); this.scene = clone(chart); this.sceneMetadata = clone(metadata); this.canvasVersion++; this.generation++; }
  setTransport(anchor, discontinuity = false) {
    if (!anchor || !Number.isFinite(anchor.songUs) || !Number.isFinite(anchor.pcMonoUs) || !['playing', 'paused'].includes(anchor.state)) throw new Error('Invalid transport');
    if (discontinuity) { this.abort('播放代次变化'); this.generation++; }
    this.transport = { ...anchor };
  }
  songAt(pcUs) { return this.transport.songUs + (this.transport.state === 'playing' ? Math.max(0, pcUs - this.transport.pcMonoUs) : 0); }
  begin() {
    if (!this.scene || this.transport.state !== 'playing') throw new Error('请先安排音频播放');
    this.generation++;
    this.take = { id: randomUUID(), sessionId: this.sessionId, generation: this.generation, canvasVersion: this.canvasVersion,
      startedUs: this.nowUs(), validStartPcUs: this.transport.pcMonoUs, anchor: clone(this.transport),
      chart: clone(this.scene), samples: [], batches: new Map(), status: 'recording', skipped: 0 };
    this.take.chartHash = this.sceneMetadata?.chartHash || null;
    this.take.media = clone(this.sceneMetadata?.media || null);
    return this.snapshot();
  }
  end(reason = '作者停止') {
    if (!this.take) return null;
    const take = this.take;
    take.validEndPcUs = this.nowUs(); take.status = 'stopped'; take.reason = reason;
    const saved = this.serializable(take);
    this.retired.set(take.id, take);
    while (this.retired.size > 4) this.retired.delete(this.retired.keys().next().value);
    this.history.push(saved); if (this.history.length > 100) this.history.shift();
    this.take = null; return saved;
  }
  abort(reason) { return this.end(reason); }
  serializable(take = this.take) {
    if (!take) return null;
    const { batches, ...data } = take;
    return clone(data);
  }
  snapshot() {
    return { type: 'SongSnapshot', protocolVersion: PROTOCOL, sessionId: this.sessionId, generation: this.generation,
      canvasVersion: this.canvasVersion, chart: this.scene, transport: this.transport,
      chartHash: this.sceneMetadata?.chartHash || null,
      takeId: this.take?.id || null, recording: !!this.take, sampleCount: this.take?.samples.length || 0 };
  }
  acceptBatch(batch, device) {
    const t = this.take?.id === batch.takeId ? this.take : this.retired.get(batch.takeId), now = this.nowUs();
    if (!t || batch.sessionId !== this.sessionId || batch.takeId !== t.id || batch.generation !== t.generation)
      throw new Error('E_GENERATION: 旧会话或未开启录制');
    if (!device.clock.model || device.clock.model.sampleCount < 3 || now - device.clock.model.measuredPcUs > 15e6)
      throw new Error('E_CLOCK: 尚未同步或同步已过期');
    if (!Number.isSafeInteger(batch.seq) || batch.seq < 0 || batch.seq > 100000 || !Array.isArray(batch.samples) || !batch.samples.length || batch.samples.length > 64)
      throw new Error('E_BATCH: 非法触控批次');
    let state = t.batches.get(device.id);
    if (!state) { state = { contiguous: -1, received: new Set() }; t.batches.set(device.id, state); }
    if (state.received.has(batch.seq)) return { type: 'Ack', takeId: t.id, generation: t.generation, contiguousSeq: state.contiguous, duplicate: true };
    if (batch.seq > state.contiguous + 128) throw new Error('E_SEQUENCE: 批次缺口过大');
    const accepted = [];
    for (const [i, s] of batch.samples.entries()) {
      if (!s || s.phase !== 'began' || s.sampleIndex !== i || !Number.isSafeInteger(s.touchId) || !Number.isFinite(s.startMonoUs) ||
        !Number.isFinite(s.x) || !Number.isFinite(s.y) || s.x < 0 || s.x > 1 || s.y < 0 || s.y > 1 ||
        s.canvasVersion !== t.canvasVersion || !Number.isSafeInteger(s.transformVersion) || !Number.isFinite(s.displaySongUs) ||
        !s.pose || !Number.isFinite(s.rawX) || !Number.isFinite(s.rawY) || !(s.viewport?.width > 0) || !(s.viewport?.height > 0))
        throw new Error('E_SAMPLE: 非法触控样本');
      const capturedModel = device.models?.get(s.clockModelId) || (s.clockModelId === device.clock.model.id ? device.clock.model : null);
      if (!capturedModel || capturedModel.sampleCount < 3) throw new Error('E_CLOCK_MODEL: 采集时钟模型不存在');
      const model = clone(capturedModel), pcMonoUs = model.a * s.startMonoUs + model.b;
      if (pcMonoUs > now + 50000 || pcMonoUs < now - 5e6 || pcMonoUs < t.validStartPcUs || (t.validEndPcUs !== undefined && pcMonoUs > t.validEndPcUs)) continue;
      const expected = cameraAt(t.chart, s.displaySongUs / 1e6);
      if (![s.pose.position?.x, s.pose.position?.y, s.pose.rotation, s.pose.scale].every(Number.isFinite) ||
        Math.abs(expected.position.x - s.pose.position.x) > .001 || Math.abs(expected.position.y - s.pose.position.y) > .001 ||
        Math.abs(expected.rotation - s.pose.rotation) > .001 || Math.abs(expected.scale - s.pose.scale) > .001)
        throw new Error('E_POSE: 平板画面姿态与谱面不一致');
      if (t.samples.length + accepted.length >= 10000) throw new Error('E_LIMIT: 本轮录入已达 10000 触点');
      const songUs = t.anchor.songUs + pcMonoUs - t.anchor.pcMonoUs;
      accepted.push({ ...clone(s), deviceId: device.id, seq: batch.seq, pcMonoUs, receivedPcUs: now,
        songUs, clockModel: model, chartPoint: unprojectPoint(normalizedToView(s.x, s.y), s.pose),
        delayedUs: now - pcMonoUs, quality: model.uncertaintyUs > 10000 ? 'review' : 'normal' });
    }
    t.samples.push(...accepted); t.skipped += batch.samples.length - accepted.length;
    state.received.add(batch.seq);
    while (state.received.has(state.contiguous + 1)) state.contiguous++;
    return { type: 'Ack', takeId: t.id, generation: t.generation, contiguousSeq: state.contiguous, accepted: accepted.length, historical: t !== this.take };
  }
  findTake(id) { return this.take?.id === id ? this.take : this.retired.get(id); }
}
