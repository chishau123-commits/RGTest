import fs from 'node:fs/promises';
import path from 'node:path';
import { randomBytes } from 'node:crypto';

export async function atomicWrite(file, text) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  const temporary = `${file}.${randomBytes(6).toString('hex')}.tmp`;
  try {
    const handle = await fs.open(temporary, 'wx');
    try { await handle.writeFile(text, 'utf8'); await handle.sync(); } finally { await handle.close(); }
    await fs.rename(temporary, file);
  } finally { await fs.rm(temporary, { force: true }).catch(() => {}); }
}
