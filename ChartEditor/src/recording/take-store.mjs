import fs from 'node:fs/promises';
import path from 'node:path';
import { atomicWrite } from '../core/storage.mjs';

// A durable append-only sample journal keeps per-touch work proportional to new samples.
// Full Take JSON is only rewritten at stop or on late backfill.
export class TakeStore {
  constructor(directory) { this.directory = directory; this.counts = new Map(); this.queue = Promise.resolve(); }
  save(take) {
    const work = this.queue.then(() => this.write(take));
    this.queue = work.catch(() => {}); return work;
  }
  async write(take) {
    if (!/^[a-f0-9-]{36}$/.test(take.id)) throw new Error('Invalid Take id');
    const prefix = path.join(this.directory, take.id), { batches, samples, ...metadata } = take;
    let count = this.counts.get(take.id);
    if (count === undefined) {
      await atomicWrite(`${prefix}.take-meta.json`, JSON.stringify(metadata));
      count = 0; this.counts.set(take.id, 0);
    }
    const pending = samples.slice(count);
    if (pending.length) {
      const handle = await fs.open(`${prefix}.take-journal.jsonl`, 'a');
      const startLength = (await handle.stat()).size;
      try { await handle.writeFile(pending.map(s => JSON.stringify(s) + '\n').join(''), 'utf8'); await handle.sync(); }
      catch (error) { await handle.truncate(startLength).catch(() => {}); throw error; }
      finally { await handle.close(); }
      this.counts.set(take.id, count + pending.length);
    }
    if (take.status !== 'recording') {
      await atomicWrite(`${prefix}.take-meta.json`, JSON.stringify(metadata));
      await atomicWrite(`${prefix}.take.json`, JSON.stringify({ ...metadata, samples }));
    }
  }
  async recover(limit = 100) {
    await this.queue;
    const files = await fs.readdir(this.directory).catch(e => { if (e.code === 'ENOENT') return []; throw e; });
    const takes = new Map();
    for (const file of files) {
      if (!/^[a-f0-9-]{36}\.take\.json$/.test(file)) continue;
      const stat = await fs.stat(path.join(this.directory, file));
      if (stat.size > 24 * 1024 * 1024) throw new Error('Take recovery exceeds size limit');
      const take = JSON.parse(await fs.readFile(path.join(this.directory, file), 'utf8')); takes.set(take.id, take);
    }
    for (const file of files) {
      if (!/^[a-f0-9-]{36}\.take-meta\.json$/.test(file)) continue;
      const id = file.slice(0, 36), metadata = JSON.parse(await fs.readFile(path.join(this.directory, file), 'utf8'));
      const journalPath = path.join(this.directory, `${id}.take-journal.jsonl`);
      const size = await fs.stat(journalPath).then(s => s.size).catch(e => { if (e.code === 'ENOENT') return 0; throw e; });
      if (size > 24 * 1024 * 1024) throw new Error('Take journal exceeds recovery size limit');
      const journal = await fs.readFile(journalPath, 'utf8').catch(e => { if (e.code === 'ENOENT') return ''; throw e; });
      const lines = journal.split('\n'), samples = [], keys = new Set();
      for (let i = 0; i < lines.length; i++) {
        if (!lines[i]) continue;
        let sample;
        try { sample = JSON.parse(lines[i]); } catch (e) { if (i === lines.length - 1 && !journal.endsWith('\n')) break; throw new Error(`Take ${id} journal is damaged`); }
        const key = `${sample.deviceId}:${sample.seq}:${sample.sampleIndex}`;
        if (!keys.has(key)) { samples.push(sample); keys.add(key); }
      }
      if (metadata.status === 'recording') { metadata.status = 'interrupted'; metadata.reason = '恢复未结束的录制'; }
      if (!takes.has(id) || samples.length >= takes.get(id).samples.length) takes.set(id, { ...metadata, samples });
    }
    return [...takes.values()].sort((a, b) => a.startedUs - b.startedUs).slice(-limit);
  }
}
