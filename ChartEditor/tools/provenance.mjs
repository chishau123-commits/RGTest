import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { extractFile } from '@electron/asar';
const root = path.resolve(import.meta.dirname, '..'), repo = path.dirname(root);
const build = path.join(repo, 'Builds/ChartEditor'), asar = path.join(build, 'win-unpacked/resources/app.asar');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const sourceHashes = {};
async function verify(relative) {
  const file = path.join(root, relative), bytes = await fs.readFile(file), bundled = extractFile(asar, relative);
  if (!bytes.equals(bundled)) throw new Error(`Packaged source differs from working source: ${relative}`);
  sourceHashes[relative.replaceAll('\\', '/')] = hash(bytes);
}
async function walk(relative) {
  for (const e of (await fs.readdir(path.join(root, relative), { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name))) {
    const next = path.join(relative, e.name); if (e.isDirectory()) await walk(next); else await verify(next);
  }
}
for (const f of ['main.cjs', 'preload.cjs']) await verify(f);
for (const dir of ['src', 'assets']) await walk(dir);
const executable = path.join(build, 'RingChartEditor-0.1.0-win-x64.exe');
const testXml = await fs.readFile(path.join(repo, 'Builds/ChartEditorEvidence/editor-tests.xml'), 'utf8');
const totalTests = (testXml.match(/<testcase\b/g) || []).length;
const failedTests = (testXml.match(/<(?:failure|error)\b/g) || []).length;
if (totalTests !== 24 || failedTests !== 0) throw new Error('Actual JUnit result is missing or failed');
const report = { version: '0.1.0', createdAt: new Date().toISOString(), sourceMatchesAsar: true,
  executable: path.relative(repo, executable).replaceAll('\\', '/'),
  bytes: (await fs.stat(executable)).size, executableSha256: hash(await fs.readFile(executable)), asarSha256: hash(await fs.readFile(asar)),
  tests: { total: totalTests, passed: totalTests - failedTests, failed: failedTests, report: 'Builds/ChartEditorEvidence/editor-tests.xml' },
  native: JSON.parse(await fs.readFile(path.join(repo, 'Builds/ChartEditorEvidence/native-profile/startup-report.json'), 'utf8')),
  portable: JSON.parse(await fs.readFile(path.join(repo, 'Builds/ChartEditorEvidence/portable-profile/startup-report.json'), 'utf8')),
  sourceHashes };
if (report.native.status !== 'ready' || report.portable.status !== 'ready') throw new Error('Startup evidence missing');
await fs.mkdir(path.join(repo, 'Docs/Evidence'), { recursive: true });
await fs.writeFile(path.join(repo, 'Docs/Evidence/chart-editor-build.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({ sourceMatchesAsar: true, sourceFiles: Object.keys(sourceHashes).length, bytes: report.bytes, sha256: report.executableSha256 }, null, 2));
