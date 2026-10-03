import fs from 'node:fs/promises';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
const root = path.resolve(import.meta.dirname, '..');
async function walk(dir) {
  for (const e of await fs.readdir(dir, { withFileTypes: true })) {
    const file = path.join(dir, e.name);
    if (e.isDirectory()) await walk(file);
    else if (/\.(mjs|cjs)$/.test(file)) {
      execFileSync(process.execPath, ['--check', file], { stdio: 'pipe' });
      const text = await fs.readFile(file, 'utf8');
      if (/\b(?:from|import)\s*['"][^'"]*(?:Assets[\\/]|RingGame[\\/]|\.\.\/\.\.\/\.\.\/)/.test(text)) throw new Error(`Game import crosses editor boundary: ${file}`);
    }
  }
}
await walk(path.join(root, 'src'));
for (const file of ['main.cjs', 'preload.cjs']) execFileSync(process.execPath, ['--check', path.join(root, file)], { stdio: 'pipe' });
const pkg = JSON.parse(await fs.readFile(path.join(root, 'package.json'), 'utf8'));
if (pkg.build.files.some(x => /Assets|RingGame|\.\.\//.test(x))) throw new Error('Packaging crosses independent editor directory');
console.log('Editor source syntax and Unity boundary checks passed.');
