// Only used by the loopback development preview in tools/preview.mjs.
// The packaged desktop application uses the isolated Electron preload API.
async function call(method, args = []) {
  const response = await fetch('/preview-rpc', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Ring-Preview': '1' }, body: JSON.stringify({ method, args }) });
  const result = await response.json();
  if (result.error) throw new Error(result.error);
  return result.value;
}
export const previewAPI = Object.fromEntries(['open', 'save', 'exportChart', 'importAudio', 'recover', 'autosave', 'clock', 'tabletStart', 'tabletStop', 'tabletCommand', 'exportTake'].map(name => [name, (...args) => call(name, args)]));
previewAPI.reportHealth = async () => true;
previewAPI.onTablet = callback => {
  const timer = setInterval(async () => { try { for (const event of await call('events')) callback(event); } catch {} }, 200);
  return () => clearInterval(timer);
};
