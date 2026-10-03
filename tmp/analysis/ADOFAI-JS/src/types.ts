export type Vec2 = [number, number];

export type ABoolean = boolean | 'Enabled' | 'Disabled' | 'true' | 'false';

export const enum TextAnchor {
  UpperLeft = 'UpperLeft',
  UpperCenter = 'UpperCenter',
  UpperRight = 'UpperRight',
  MiddleLeft = 'MiddleLeft',
  MiddleCenter = 'MiddleCenter',
  MiddleRight = 'MiddleRight',
  LowerLeft = 'LowerLeft',
  LowerCenter = 'LowerCenter',
  LowerRight = 'LowerRight',
}

export const enum Hitbox {
  None = 'None',
  Kill = 'Kill',
  PassThrough = 'PassThrough',
  NoEffect = 'NoEffect',
}

export const enum FilterType {
  Grayscale = 'Grayscale',
  Sepia = 'Sepia',
  Invert = 'Invert',
  Pixellate = 'Pixellate',
  Blur = 'Blur',
  Glitch = 'Glitch',
  Bloom = 'Bloom',
  VHS = 'VHS',
  Warp = 'Warp',
  RadialBlur = 'RadialBlur',
  Custom = 'Custom',
}

export const enum FlashStyle {
  Flash = 'Flash',
  Reverse = 'Reverse',
  StayBlack = 'StayBlack',
  Kill = 'Kill',
  FlashEx = 'FlashEx',
}

export const enum RelativeTo {
  Tile = 'Tile',
  LastPosition = 'LastPosition',
  Player = 'Player',
}

export const enum TargetPlanet {
  All = 'All',
  Current = 'Current',
  Specific = 'Specific',
}

export const enum AngleCorrectionDir {
  None = 'None',
  CW = 'CW',
  CCW = 'CCW',
}

export const enum InputEventState {
  Subscribe = 'Subscribe',
  Unsubscribe = 'Unsubscribe',
}

export const enum InputEventTarget {
  Pressed = 'Pressed',
  Released = 'Released',
  Held = 'Held',
  Neutral = 'Neutral',
}

export const enum Condition {
  IfPassed = 'IfPassed',
  IfFailed = 'IfFailed',
}

export const enum BgDisplayMode {
  FitToScreen = 'FitToScreen',
  Unscaled = 'Unscaled',
  Tiled = 'Tiled',
}

export const enum BgShapeType {
  Disabled = 'Disabled',
  Tile = 'Tile',
  Circle = 'Circle',
  Diamond = 'Diamond',
  Triangle = 'Triangle',
  Hexagon = 'Hexagon',
  Donut = 'Donut',
  Pentagon = 'Pentagon',
  Custom = 'Custom',
}

export const enum HitsoundType {
  Kick = 'Kick',
  Snare = 'Snare',
  Hat = 'Hat',
  Clap = 'Clap',
  Custom = 'Custom',
}

export const enum HoldMidSoundTimingRelativeTo {
  Start = 'Start',
  End = 'End',
}

export function isEventEnabled(value: ABoolean | undefined, defaultValue: boolean): boolean {
  if (value === undefined) return defaultValue;
  if (typeof value === 'boolean') return value;
  return value === 'Enabled' || value === 'true';
}

export type TileReferenceType = 'ThisTile' | 'Start' | 'End';
export type TileReference = [number, TileReferenceType];

export function resolveTileReference(
  relativeTo: TileReference | undefined,
  thisTileId: number,
  totalTiles: number
): number {
  if (!relativeTo) return thisTileId;
  const [offset, type] = relativeTo;
  let result: number;
  switch (type) {
    case 'Start':
      result = offset;
      break;
    case 'End':
      result = totalTiles - 1 + offset;
      break;
    case 'ThisTile':
    default:
      result = thisTileId + offset;
      break;
  }
  return Math.max(0, Math.min(result, totalTiles - 1));
}

export type Vec2Like = [number, number] | { x: number; y: number };

export function normalizeVec2(v: Vec2Like | undefined): [number, number] {
  if (!v) return [0, 0];
  if (Array.isArray(v)) return [v[0] ?? 0, v[1] ?? 0];
  return [v.x ?? 0, v.y ?? 0];
}
