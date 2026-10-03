import { PPQ, clone, uniqueId } from './chart.mjs';

function checkTiming({ tick, durationTicks }) {
  if (!Number.isSafeInteger(tick) || tick < 0 || !Number.isSafeInteger(durationTicks) || durationTicks < 0 || !Number.isSafeInteger(tick + durationTicks))
    throw new Error('运镜起点与持续 tick 必须是非负安全整数');
}
function assertCameraSlot(chart, timing, ignoreId) {
  checkTiming(timing);
  const { tick, durationTicks } = timing;
  const conflicts = chart.actions.filter(a => a.id !== ignoreId &&
    (a.tick === tick || tick < a.tick + a.durationTicks && a.tick < tick + durationTicks));
  if (conflicts.length) throw new Error(`运镜 ${tick} → ${tick + durationTicks} 与 ${conflicts.map(a => `${a.id}（${a.tick} → ${a.tick + a.durationTicks}）`).join('、')} 冲突；请选择已有动作修改，或移到空闲时段。当前编辑未提交。`);
}
export function addCamera(chart, { tick, pose, durationTicks = PPQ }) {
  assertCameraSlot(chart, { tick, durationTicks });
  if (chart.actions.length >= 2000) throw new Error('运镜最多 2000 个');
  const action = { id: uniqueId(chart, 'cam'), eventType: 'MoveCamera', tick, durationTicks,
    position: clone(pose.position), rotation: pose.rotation, scale: pose.scale, ease: 'smooth' };
  chart.actions.push(action);
  return action;
}
export function setCameraTiming(chart, id, change) {
  const action = chart.actions.find(a => a.id === id);
  if (!action) throw new Error(`找不到运镜 ${id}`);
  const timing = { tick: change.tick ?? action.tick, durationTicks: change.durationTicks ?? action.durationTicks };
  assertCameraSlot(chart, timing, id);
  Object.assign(action, timing);
}
// Plan first: preserve every action and pose; only move conflicting start ticks forward.
// Equal ticks keep document order. The caller applies the plan in one History transaction.
export function planCameraRepair(chart) {
  const ids = new Set();
  for (const a of chart.actions) {
    checkTiming(a);
    if (typeof a.id !== 'string' || !a.id || ids.has(a.id)) throw new Error('运镜 ID 必须非空且唯一，不能自动猜测修复');
    ids.add(a.id);
  }
  const moves = [];
  let lastStart = -1, lastEnd = -1;
  for (const a of [...chart.actions].sort((a, b) => a.tick - b.tick)) {
    const tick = Math.max(a.tick, lastEnd, lastStart + 1);
    checkTiming({ tick, durationTicks: a.durationTicks });
    if (tick !== a.tick) moves.push({ id: a.id, from: a.tick, to: tick, durationTicks: a.durationTicks });
    lastStart = tick; lastEnd = tick + a.durationTicks;
  }
  return moves;
}
