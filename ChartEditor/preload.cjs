const { contextBridge, ipcRenderer } = require('electron');
const invoke = (channel, ...args) => ipcRenderer.invoke(channel, ...args);
contextBridge.exposeInMainWorld('editorAPI', {
  open: () => invoke('editor:open'),
  save: (project, saveAs) => invoke('editor:save', project, saveAs),
  exportChart: text => invoke('editor:export', text),
  importAudio: () => invoke('editor:audio'),
  recover: () => invoke('editor:recover'),
  autosave: project => invoke('editor:autosave', project),
  clock: () => invoke('editor:clock'),
  reportHealth: report => invoke('editor:health', report),
  tabletStart: () => invoke('editor:tablet-start'),
  tabletStop: () => invoke('editor:tablet-stop'),
  tabletCommand: command => invoke('editor:tablet-command', command),
  exportTake: take => invoke('editor:take-export', take),
  onTablet: callback => { const listener = (_, event) => callback(event); ipcRenderer.on('editor:tablet-event', listener); return () => ipcRenderer.removeListener('editor:tablet-event', listener); },
  onBeforeClose: callback => ipcRenderer.on('editor:before-close', () => { Promise.resolve().then(callback).then(() => ipcRenderer.send('editor:close-ready')).catch(error => ipcRenderer.send('editor:close-failed', error.message)); })
});
