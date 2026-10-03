const { app, BrowserWindow, ipcMain, dialog, protocol, net, session } = require('electron');
const path = require('node:path');
const fs = require('node:fs/promises');
const { pathToFileURL } = require('node:url');
protocol.registerSchemesAsPrivileged([{ scheme: 'ring-editor', privileges: { standard: true, secure: true, supportFetchAPI: true } }]);
let window, host, projectPath = null, writeQueue = Promise.resolve();
const root = __dirname;
const verificationDir = process.argv.find(a => a.startsWith('--verification-dir='))?.slice('--verification-dir='.length);
if (verificationDir && path.isAbsolute(verificationDir)) app.setPath('userData', verificationDir);
const verification = process.argv.includes('--verify-startup');
const primaryInstance = app.requestSingleInstanceLock();
if (!primaryInstance) app.quit();
app.on('second-instance', () => { if (window) { window.show(); window.focus(); } });
const serializedWrite = work => { const result = writeQueue.then(work); writeQueue = result.catch(() => {}); return result; };
app.whenReady().then(async () => {
  if (!primaryInstance) return;
  const { TabletHost } = await import('./src/recording/host.mjs');
  const { atomicWrite } = await import('./src/core/storage.mjs');
  const { TakeStore } = await import('./src/recording/take-store.mjs');
  const { validateProject } = await import('./src/core/project.mjs');
  const { parseStrict, validateChart } = await import('./src/core/chart.mjs');
  const dataDir = app.getPath('userData');
  const recovery = path.join(dataDir, 'recovery.ringproject');
  const takeStore = new TakeStore(path.join(dataDir, 'takes'));
  const validateSize = (value, max = 200 * 1024 * 1024) => {
    const text = JSON.stringify(value); if (Buffer.byteLength(text) > max) throw new Error('文件超出大小限制'); return text;
  };
  protocol.handle('ring-editor', request => {
    const url = new URL(request.url);
    if (url.hostname !== 'app') return new Response('Not found', { status: 404 });
    let pathname;
    try { pathname = decodeURIComponent(url.pathname); } catch { return new Response('Bad path', { status: 400 }); }
    const file = path.resolve(root, '.' + pathname);
    const relative = path.relative(root, file);
    if (relative.startsWith('..') || path.isAbsolute(relative) || !(relative.startsWith('src' + path.sep) || relative.startsWith('assets' + path.sep)))
      return new Response('Not found', { status: 404 });
    return net.fetch(pathToFileURL(file).toString());
  });
  session.defaultSession.setPermissionRequestHandler((_, __, callback) => callback(false));
  window = new BrowserWindow({ width: 1540, height: 1000, minWidth: 1180, minHeight: 800, backgroundColor: '#10141e',
    title: 'Ring Chart Editor', autoHideMenuBar: true,
    webPreferences: { preload: path.join(root, 'preload.cjs'), contextIsolation: true, nodeIntegration: false, sandbox: true, backgroundThrottling: false } });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', event => event.preventDefault());
  const handle = (channel, fn) => ipcMain.handle(channel, async (event, ...args) => {
    if (event.sender !== window.webContents || !event.senderFrame?.url.startsWith('ring-editor://app/')) throw new Error('Invalid IPC sender');
    return fn(...args);
  });
  host = new TabletHost({ root, onEvent: event => { if (!window.isDestroyed()) window.webContents.send('editor:tablet-event', event); },
    persistTake: take => takeStore.save(take) });
  handle('editor:clock', () => performance.now() * 1000);
  handle('editor:health', async report => {
    if (verification && report.status === 'ready') {
      const info = await host.start(0, '127.0.0.1');
      report.tabletRoutes = {};
      for (const route of ['/', '/tablet.mjs', '/tablet.css', '/core/chart.mjs', '/ui/render.mjs']) {
        const response = await fetch(`http://127.0.0.1:${info.port}${route}`);
        report.tabletRoutes[route] = response.status;
        if (response.status !== 200) throw new Error('Packaged tablet client missing: ' + route);
        await response.arrayBuffer();
      }
    }
    const text = validateSize(report, 4096);
    await serializedWrite(() => atomicWrite(path.join(dataDir, 'startup-report.json'), text));
    if (verification) setTimeout(() => app.quit(), 500);
    return true;
  });
  handle('editor:open', async () => {
    const result = await dialog.showOpenDialog(window, { filters: [{ name: '制谱器工程 / 游戏谱面', extensions: ['ringproject', 'json'] }], properties: ['openFile'] });
    if (result.canceled) return null;
    const file = result.filePaths[0], stat = await fs.stat(file);
    if (stat.size > 200 * 1024 * 1024) throw new Error('文件超过 200 MiB');
    const text = await fs.readFile(file, 'utf8');
    const project = file.endsWith('.ringproject') ? validateProject(JSON.parse(text)) : null;
    const chart = project ? project.chart : parseStrict(text);
    const repairable = new Set(['E_TIME', 'E_ACTION', 'E_TRANSFORM', 'E_GEOMETRY', 'E_GROUP', 'E_REFERENCE', 'E_AUDIO_TIME', 'E_EMPTY']);
    const errors = validateChart(chart, { allowEmpty: true }).filter(x => x.severity === 'error' && (!project || !repairable.has(x.code)));
    if (errors.length) throw new Error(errors.map(x => `${x.path}: ${x.message}`).join('\n'));
    projectPath = project ? file : null;
    return { project, chart, path: file };
  });
  handle('editor:save', async (project, saveAs) => {
    validateProject(project); const text = validateSize(project);
    let file = projectPath;
    if (!file || saveAs) {
      const result = await dialog.showSaveDialog(window, { defaultPath: file || '未命名.ringproject', filters: [{ name: '制谱器工程', extensions: ['ringproject'] }] });
      if (result.canceled) return null;
      file = result.filePath;
    }
    await serializedWrite(() => atomicWrite(file, text)); projectPath = file; return file;
  });
  handle('editor:export', async text => {
    const chart = parseStrict(text), errors = validateChart(chart).filter(x => x.severity === 'error');
    if (errors.length) throw new Error(errors.map(x => x.message).join('\n'));
    const result = await dialog.showSaveDialog(window, { defaultPath: 'prototype-chart.json', filters: [{ name: '游戏谱面 JSON', extensions: ['json'] }] });
    if (result.canceled) return null;
    await serializedWrite(() => atomicWrite(result.filePath, text)); return result.filePath;
  });
  handle('editor:audio', async () => {
    const result = await dialog.showOpenDialog(window, { filters: [{ name: '音频', extensions: ['wav', 'mp3', 'ogg', 'm4a'] }], properties: ['openFile'] });
    if (result.canceled) return null;
    const file = result.filePaths[0], stat = await fs.stat(file);
    if (stat.size > 128 * 1024 * 1024) throw new Error('音频超过 128 MiB，请先裁剪');
    return { name: path.basename(file), base64: (await fs.readFile(file)).toString('base64') };
  });
  handle('editor:autosave', p => { validateProject(p); const text = validateSize(p); return serializedWrite(() => atomicWrite(recovery, text)); });
  handle('editor:recover', async () => {
    let project = null;
    try { project = validateProject(JSON.parse(await fs.readFile(recovery, 'utf8'))); } catch (e) { if (e.code !== 'ENOENT') throw e; }
    return { project, takes: await takeStore.recover() };
  });
  handle('editor:tablet-start', async () => {
    const info = await host.start();
    const QRCode = require('qrcode');
    return { ...info, qr: await QRCode.toDataURL(info.urls[0], { width: 170, margin: 1 }) };
  });
  handle('editor:tablet-stop', async () => { await host.stop(); return host.info(); });
  handle('editor:tablet-command', async m => {
    validateSize(m, 2 * 1024 * 1024);
    if (m?.type === 'scene' && validateChart(m.chart, { allowEmpty: true }).some(x => x.severity === 'error')) throw new Error('谱面无效，无法同步到平板');
    return host.command(m);
  });
  handle('editor:take-export', async take => {
    const text = validateSize(take, 24 * 1024 * 1024);
    const result = await dialog.showSaveDialog(window, { defaultPath: `录入-${take.id?.slice(0, 8) || 'draft'}.take.json`, filters: [{ name: '原始 Take', extensions: ['json'] }] });
    if (result.canceled) return null;
    await serializedWrite(() => atomicWrite(result.filePath, text)); return result.filePath;
  });
  await window.loadURL('ring-editor://app/src/ui/index.html');
  let closing = false;
  const finishClose = () => host.stop().then(() => writeQueue).finally(() => window.destroy());
  ipcMain.on('editor:close-ready', event => { if (event.sender === window.webContents && closing) finishClose(); });
  ipcMain.on('editor:close-failed', (event, message) => {
    if (event.sender === window.webContents) { closing = false; dialog.showErrorBox('关闭前保存失败', String(message).slice(0, 500)); }
  });
  window.on('close', event => {
    if (closing) return;
    event.preventDefault();
    closing = true;
    if (verification) finishClose();
    else window.webContents.send('editor:before-close');
  });
});
app.on('window-all-closed', () => app.quit());
