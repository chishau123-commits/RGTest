export class AudioTransport {
  constructor() {
    this.context = new AudioContext({ latencyHint: 'interactive' }); this.buffer = null;
    this.source = null; this.playing = false; this.cursor = 0; this.startContext = 0; this.startSong = 0;
    this.outputEstimate = 'baseLatency';
  }
  async load(bytes) { this.pause(); this.buffer = await this.context.decodeAudioData(bytes.slice(0)); this.cursor = 0; return this.buffer; }
  get duration() { return this.buffer?.duration || 0; }
  outputContextNow() {
    const t = this.context.getOutputTimestamp?.();
    if (t?.contextTime > 0 && t.performanceTime > 0) { this.outputEstimate = 'getOutputTimestamp'; return t.contextTime + (performance.now() - t.performanceTime) / 1000; }
    return this.context.currentTime - (this.context.baseLatency || 0);
  }
  now() { return this.playing ? Math.min(this.duration, this.startSong + Math.max(0, this.outputContextNow() - this.startContext)) : this.cursor; }
  async play(delay = .08) {
    if (!this.buffer) throw new Error('请先导入音频');
    await this.context.resume();
    this.stopSource();
    if (this.cursor >= this.duration - .01) this.cursor = 0;
    this.startSong = this.cursor; this.startContext = this.context.currentTime + delay;
    this.source = this.context.createBufferSource(); this.source.buffer = this.buffer;
    this.source.connect(this.context.destination); this.source.start(this.startContext, this.startSong);
    this.playing = true;
  }
  pause() { this.cursor = this.now(); this.playing = false; this.stopSource(); }
  seek(seconds) { this.pause(); this.cursor = Math.max(0, Math.min(this.duration || 600, seconds)); }
  stopSource() { if (this.source) { try { this.source.stop(); } catch {} this.source.disconnect(); this.source = null; } }
  anchor(hostOffsetUs) {
    if (!this.playing) return { state: 'paused', songUs: this.cursor * 1e6, pcMonoUs: performance.now() * 1000 + hostOffsetUs };
    const t = this.context.getOutputTimestamp?.();
    if (t?.contextTime > 0 && t.performanceTime > 0) this.outputEstimate = 'getOutputTimestamp';
    const audiblePerf = t?.contextTime > 0 && t.performanceTime > 0 ?
      t.performanceTime + (this.startContext - t.contextTime) * 1000 :
      performance.now() + (this.startContext - this.context.currentTime + (this.context.baseLatency || 0)) * 1000;
    return { state: 'playing', songUs: this.startSong * 1e6, pcMonoUs: audiblePerf * 1000 + hostOffsetUs,
      audioEstimate: this.outputEstimate, baseLatencyMs: (this.context.baseLatency || 0) * 1000 };
  }
  tickClick() {
    const osc = this.context.createOscillator(), gain = this.context.createGain();
    osc.frequency.value = 1400; gain.gain.setValueAtTime(.12, this.context.currentTime);
    gain.gain.exponentialRampToValueAtTime(.001, this.context.currentTime + .035);
    osc.connect(gain); gain.connect(this.context.destination); osc.start(); osc.stop(this.context.currentTime + .04);
  }
}
