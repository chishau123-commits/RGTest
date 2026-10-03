# ADOFAI

A zero-dependency JavaScript/TypeScript library for parsing, editing, and exporting ADOFAI level files. Fully browser-compatible.

## Features

- **Multiple Parsers** — `StringParser`, `BufferParser`, `ArrayBufferParser`, `LargeFileParser` for incremental large-file parsing
- **Level Management** — load, edit, and export `.adofai` files with full tile and event access
- **Typed Events** — 57 typed event interfaces covering all ADOFAI event types
- **Shared Types** — const enums and utility types for ADOFAI-specific values (angles, hitboxes, filters, etc.)
- **PathData Conversion** — convert between pathData string and angleData array
- **Effect Filtering** — preset and custom event filtering (clear effects, keep/exclude events)
- **Precompute Mode** — batch-process and cache progress events for rendering pipelines
- **Lightweight Data** — memory-efficient tile data extraction for large levels

## Installation

```bash
npm install adofai
# or
yarn add adofai
# or
pnpm add adofai
```

## Import

**ESM:**
```ts
import * as adofai from 'adofai';
import { Level, Parsers, Types, Events, Structure } from 'adofai';
```

**CommonJS:**
```ts
const adofai = require('adofai');
```

**Subpath imports:**
```ts
import { StringParser } from 'adofai/parser/string';
import { BufferParser } from 'adofai/parser/buffer';
import { ArrayBufferParser } from 'adofai/parser/array-buffer';
import * as Types from 'adofai/types';
import * as Events from 'adofai/event';
```

---

## Parsers

Four parsers handle different input formats. All are zero-dependency and browser-compatible.

### StringParser

Parses ADOFAI JSON from a string. Handles non-standard formatting (trailing commas, raw newlines in strings).

```ts
import { StringParser } from 'adofai';

const parser = new StringParser();
const data = parser.parse(`{ "angleData": [...], "settings": {...}, "actions": [...] }`);
```

### BufferParser (Uint8Array)

Parses ADOFAI JSON directly from a `Uint8Array` binary stream using a byte-level state machine. No intermediate string conversion, handles BOM stripping automatically.

```ts
import { BufferParser } from 'adofai';

const parser = new BufferParser();
const u8 = new Uint8Array(await file.arrayBuffer());
const data = parser.parse(u8);
```

### ArrayBufferParser

Accepts `ArrayBuffer` or `string`. Handles BOM stripping, trailing comma normalization, and UTF-8 decoding.

```ts
import { ArrayBufferParser } from 'adofai';

const parser = new ArrayBufferParser();
const buffer = await response.arrayBuffer();
const data = parser.parse(buffer);
```

### LargeFileParser

Memory-optimized parser for large `.adofai` files. Scans raw bytes to find JSON root properties, then parses sections incrementally without loading the entire file into a JS string. Ideal for files with massive `angleData` or `action` arrays.

```ts
import { LargeFileParser } from 'adofai';

const parser = new LargeFileParser((stage, percent) => {
  console.log(`[${stage}] ${percent}%`);
}, {
  skipLargeActions: false, // skip actions if > 100MB
  maxActions: 0            // limit parsed actions count
});

const result = parser.parse(arrayBuffer);
// result: { settings?, angleData?, pathData?, actions?, decorations? }
```

Key behaviors:
- **< 50MB** — sections are fully JSON.parsed normally.
- **> 50MB** — `actions` is parsed incrementally (each object parsed independently).
- **> 100MB** — actions can be skipped entirely via `skipLargeActions: true`.
- **Any size** — `angleData` is always parsed incrementally (number-by-number).
- BOM is automatically stripped.

---

## Level

The `Level` class is the core data structure. It accepts ADOFAI file content (string, object, ArrayBuffer, Uint8Array, or Buffer) and provides tile management and export.

### Create & Load

```ts
import { Level } from 'adofai';

// From string
const level = new Level(adofaiJsonString);
await level.load();

// With custom parser provider
const level = new Level(rawData, bufferParser);
await level.load();

// From already-parsed object
const level = new Level({
  angleData: [...],
  settings: { ... },
  actions: [...],
  decorations: [...]
});
await level.load();

// Event-based loading
level.on('load', () => {
  console.log('Level loaded:', level.tiles.length, 'tiles');
});
level.load();

// Progress events
level.on('parse:progress', (event) => {
  // { stage: 'relativeAngle', current: 500, total: 1000, percent: 50 }
  console.log(`${event.stage}: ${event.percent}%`);
});
```

Progress stages: `start` → `pathData` | `angleData` → `relativeAngle` → `tilePosition` → `complete`

### Data Model Overview

After loading, the level data is organized into two layers:

```
┌──────────────────────────────────────────┐
│  Source Data (read-only initial values)   │
│  level.angleData       — raw angle array  │
│  level.actions         — flat event list  │
│  level.__decorations   — flat deco list   │
│  level.settings        — level settings   │
├──────────────────────────────────────────┤
│  Working Data (primary operation target)  │
│  level.tiles           — Tile[]          │
└──────────────────────────────────────────┘
```

**`level.tiles` is where all data operations happen.** The source arrays (`angleData`, `actions`, `decorations`) are initial inputs and are **not** kept in sync when you modify tiles. When you export, `angleData`, `actions`, and `decorations` are reconstructed from `level.tiles`.

### Tile Structure

Each tile in `level.tiles` has the following structure:

```ts
interface Tile {
  direction?: number;       // Original angle data value (incl. 999)
  angle?: number;           // Computed relative angle (for rendering)
  _lastdir?: number;        // Previous tile's direction
  twirl?: number;           // Accumulated twirl count up to this tile
  actions: ActionData[];    // Events belonging to this tile
  addDecorations?: ActionData[]; // Decorations on this tile
  position?: number[];      // Computed [x, y] position
  extraProps?: Record<string, any>; // Extra computed data (angle1, angle2, cangle)
}
```

### Working with Tiles

**Read tile data:**
```ts
// Total tile count
level.tiles.length;

// Access a specific tile
const tile = level.tiles[42];
tile.direction;       // raw angle value
tile.angle;           // relative angle
tile.actions;         // events on this tile
tile.addDecorations;  // decorations on this tile
tile.twirl;           // twirl count
tile.position;        // [x, y] (after calculateTilePosition)
```

**Modify tiles:**
```ts
// Append a new tile
level.floorOperation({ type: 'append', direction: 180 });

// Insert at specific index
level.floorOperation({ type: 'insert', direction: 90, id: 10 });

// Delete a tile
level.floorOperation({ type: 'delete', id: 10 });
```

**Query events across tiles:**
```ts
// Find all tiles with a specific event type
const results = level.filterActionsByEventType('Flash');
// returns { index: number, action: ActionData }[]

// Get events at a specific tile index
const { count, actions } = level.getActionsByIndex('MoveTrack', 5);
```

### Calculate Tile Positions

Populates `tile.position` and `tile.extraProps` for all tiles.

```ts
const positions = level.calculateTilePosition();
// returns number[][] — [x, y] for each tile including endpoint

// After this call, each tile.position is set
level.tiles[5].position;  // [x, y]
level.tiles[5].extraProps; // { angle1, angle2, cangle }
```

### Effect Filtering (operates on tiles)

All effect operations modify `level.tiles` in-place.

```ts
import { Presets } from 'adofai';

// Using a preset
level.clearEffect('preset_noeffect');

// Custom filter — keep only specific events
level.clearEvent({ type: 'include', events: ['SetSpeed', 'Twirl'] });

// Custom filter — exclude specific events
level.clearEvent({ type: 'exclude', events: ['Flash', 'Bloom'] });

// Clear all decorations from all tiles
level.clearDeco();
```

### Export (reconstructs from tiles)

```ts
// Export as formatted ADOFAI JSON string
const str = level.export('string', 0, true);
// fs.writeFileSync('output.adofai', str);

// Export as object
const obj = level.export('object', 0, true);
// { angleData, settings, actions, decorations }
// All three arrays are reconstructed from level.tiles
```

### Event System

```ts
// Listen to lifecycle events
const guid = level.on('load', (level) => { /* ... */ });

// Remove listener by GUID
level.off(guid);

// Trigger custom events
level.trigger('custom:event', data);
```

---

## Types

Shared ADOFAI-specific types and const enums, exported via `Types.*` or `adofai/types`.

### Const Enums

```ts
import { Types } from 'adofai';

Types.TextAnchor.UpperLeft       // 'UpperLeft'
Types.Hitbox.None                // 'None'
Types.FilterType.Bloom           // 'Bloom'
Types.FlashStyle.Flash           // 'Flash'
Types.RelativeTo.Tile            // 'Tile'
Types.TargetPlanet.All           // 'All'
Types.AngleCorrectionDir.CW      // 'CW'
Types.InputEventState.Subscribe  // 'Subscribe'
Types.InputEventTarget.Pressed   // 'Pressed'
Types.Condition.IfPassed         // 'IfPassed'
Types.BgDisplayMode.FitToScreen  // 'FitToScreen'
Types.BgShapeType.Circle         // 'Circle'
Types.HitsoundType.Kick          // 'Kick'
Types.HoldMidSoundTimingRelativeTo.Start // 'Start'
```

### Utility Types

```ts
Types.Vec2                       // [number, number]
Types.ABoolean                   // boolean | 'Enabled' | 'Disabled' | 'true' | 'false'
Types.TileReference              // [number, TileReferenceType]
Types.TileReferenceType          // 'ThisTile' | 'Start' | 'End'
Types.Vec2Like                   // [number, number] | { x: number; y: number }
```

### Utility Functions

```ts
// Evaluate ABoolean values
Types.isEventEnabled(value, defaultValue);  // boolean

// Resolve relative tile references
Types.resolveTileReference([2, 'End'], currentTileId, totalTiles);
// = totalTiles - 1 + 2

Types.resolveTileReference([-1, 'ThisTile'], 5, 100);
// = 4

// Normalize ADOFAI position formats (array and object forms)
Types.normalizeVec2([3, 5]);           // [3, 5]
Types.normalizeVec2({ x: 3, y: 5 });   // [3, 5]
```

---

## PathData

Convert pathData strings (e.g. `"REJW"`) into angleData arrays.

```ts
import { pathData } from 'adofai';

// Character → angle mapping table
pathData.pathDataTable;
// { R: 0, p: 15, J: 30, E: 45, T: 60, ... }

// Convert path string to angle numbers
const angleData = pathData.parseToangleData("REJW");
// [0, 45, 30, 165]
```

---

## Event Interfaces

57 typed event interfaces covering all ADOFAI event types. Each extends `AdofaiEvent` with `floor` and `eventType` fields.

```ts
import type { Events } from 'adofai';

// Each event type has a matching interface:
const flash: Events.Flash = {
  floor: 0,
  eventType: 'Flash',
  duration: 1,
  color: 'ffffff',
  flashStyle: Types.FlashStyle.Flash,
  opacity: 100,
};

const setSpeed: Events.SetSpeed = {
  floor: 0,
  eventType: 'SetSpeed',
  speed: 1.0,
};

const moveTrack: Events.MoveTrack = {
  floor: 5,
  eventType: 'MoveTrack',
  duration: 4,
  positionOffset: [0, 3],
  rotation: 90,
  easing: 'Linear',
};
```

Full list of event types: `SetSpeed`, `Twirl`, `Checkpoint`, `MoveCamera`, `CustomBackground`, `ChangeTrack`, `ColorTrack`, `AnimateTrack`, `RecolorTrack`, `MoveTrack`, `SetText`, `Flash`, `SetHitsound`, `SetFilter`, `SetFilterAdvanced`, `SetPlanetRotation`, `HallOfMirrors`, `ShakeScreen`, `MoveDecorations`, `PositionTrack`, `RepeatEvents`, `Bloom`, `Hold`, `SetHoldSound`, `SetConditionalEvents`, `ScreenTile`, `ScreenScroll`, `EditorComment`, `Bookmark`, `CallMethod`, `AddComponent`, `PlaySound`, `MultiPlanet`, `FreeRoam`, `FreeRoamTwirl`, `FreeRoamRemove`, `Pause`, `AutoPlayTiles`, `Hide`, `ScaleMargin`, `ScaleRadius`, `Multitap`, `TileDimensions`, `KillPlayer`, `ScalePlanets`, `SetFloorIcon`, `AddDecoration`, `AddText`, `AddObject`, `SetObject`, `SetDefaultText`, `SetFrameRate`, `AddParticle`, `SetParticle`, `EmitParticle`, `SetInputEvent`.

---

---

## Effect Presets

## Advanced: Precompute Mode

For rendering pipelines that need deterministic event replay. Instead of firing progress events during load, events are cached and can be polled.

```ts
level.enablePrecomputeMode();
await level.load();
level.calculateTilePosition();

const events = level.getPrecomputedEvents();
// { start: [...], pathData: [...], angleData: [...], ... }

// Get events up to a specific progress percentage
const at50 = level.getEventsAtPercent(50);
```

---

## Advanced: Lightweight Tile Data

For large levels where storing full `Tile[]` objects is memory-prohibitive. Precomputes only rendering-essential data.

```ts
// Precompute lightweight data (angles + positions + twirl flags)
level.precomputeLightweight();

// Access the compact data
const data = level.getLightweightData();
// { totalTiles, angles: number[], positions: [number,number][], twirlFlags: boolean[] }

// Get a range of tiles (chunked access)
const chunk = level.getLightweightDataRange(0, 100);
// { angles: [...], positions: [...], twirlFlags: [...] }

// Get single tile render data
const tile = level.getTileRenderData(42);
// { angle, position, hasTwirl }
```

---

## Structure Interfaces

Available via `adofai/structure`:

```ts
import type { AdofaiEvent, LevelOptions, Tile, ParseProgressEvent } from 'adofai/structure';

// AdofaiEvent: { floor: number, eventType: string, [key: string]: any }
// LevelOptions: { pathData?: string, angleData?: number[], actions, settings, decorations }
// Tile: { direction?, angle?, actions, addDecorations?, position?, ... }
// ParseProgressEvent: { stage, current, total, percent, data? }
```

---

## Package Exports

| Import path | Contents |
|---|---|
| `adofai` | Main: Level, Parsers, Types, Events, Structure, Presets, pathData |
| `adofai/parser` | Parser classes |
| `adofai/parser/string` | StringParser |
| `adofai/parser/buffer` | BufferParser |
| `adofai/parser/array-buffer` | ArrayBufferParser |
| `adofai/types` | Types (const enums, utilities) |
| `adofai/event` | All event type interfaces |
| `adofai/structure` | Core interfaces (LevelOptions, Tile, etc.) |
| `adofai/filter` | Filter presets |
| `adofai/filter/effect-processor` | Low-level effect processor |
| `adofai/pathdata` | PathData conversion table |
| `adofai/types` | Utility types and functions |

---

## License

BSD-3-Clause
