// Read-only DOM geometry checks for --verify-startup; no screenshot/visual verification implied.
function overlapping(rects) {
  let count = 0;
  for (let i = 0; i < rects.length; i++) for (let j = i + 1; j < rects.length; j++) {
    const a = rects[i], b = rects[j];
    if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1) count++;
  }
  return count;
}
export function verifyLayout(doc, { innerHeight, devicePixelRatio }) {
  const rect = e => e.getBoundingClientRect(), compact = innerHeight <= 880;
  const toolbar = [...doc.querySelectorAll('.timeline-options>label')].map(rect);
  const transport = [...doc.querySelectorAll('.transport button,.transport strong,.transport .muted')].map(rect);
  const cards = [...doc.querySelectorAll('.action-row,.take-row')].filter(e => rect(e).height > 0);
  const crampedCards = cards.filter(e => {
    const title = rect(e.querySelector('strong')), meta = rect(e.querySelector('.muted')), controls = rect(e.querySelector('.card-controls'));
    return rect(e).height < 64 || title.bottom > meta.top + 1 || overlapping([title, meta, controls]) > 0;
  }).length;
  const scroll = doc.getElementById('timeline-scroll'), info = doc.querySelector('.bottom-panel'), canvas = doc.getElementById('timeline');
  const view = rect(scroll), surface = rect(canvas);
  const previousScroll = { x: scroll.scrollLeft, y: scroll.scrollTop };
  scroll.scrollLeft = 400; scroll.scrollTop = 80;
  const scrolledSurface = rect(canvas), canvasRemainsFixedOnScroll = Math.abs(scrolledSurface.left - surface.left) <= 1 && Math.abs(scrolledSurface.top - surface.top) <= 1;
  scroll.scrollLeft = previousScroll.x; scroll.scrollTop = previousScroll.y;
  const result = { compact, viewport: { width: doc.documentElement.clientWidth, height: innerHeight },
    toolbarOverlaps: overlapping(toolbar), transportOverlaps: overlapping(transport), crampedCards,
    actionCards: cards.filter(e => e.classList.contains('action-row')).length,
    takeCards: cards.filter(e => e.classList.contains('take-row')).length,
    timelineHeight: Math.round(view.height), informationHeight: Math.round(rect(info).height), canvasRemainsFixedOnScroll,
    virtualCanvas: canvas.width <= Math.ceil(scroll.clientWidth * Math.min(devicePixelRatio || 1, 2)) + 2 &&
      canvas.height <= Math.ceil(scroll.clientHeight * Math.min(devicePixelRatio || 1, 2)) + 2,
    canvasFillsViewport: Math.abs(surface.width - scroll.clientWidth) <= 1 && Math.abs(surface.height - scroll.clientHeight) <= 1 };
  if (result.toolbarOverlaps || result.transportOverlaps || crampedCards || !cards.length ||
    result.timelineHeight < (compact ? 158 : 188) || result.informationHeight < (compact ? 128 : 158) ||
    !result.virtualCanvas || !result.canvasFillsViewport || !canvasRemainsFixedOnScroll) throw new Error(`下半栏几何检查失败：${JSON.stringify(result)}`);
  return result;
}
