const BOM = new Uint8Array([0xef, 0xbb, 0xbf]);

/** 共享的解码器实例，避免在热路径中反复创建 */
const SHARED_DECODER = new TextDecoder('utf-8');

export interface LargeFileParseResult {
  settings?: any;
  angleData?: number[];
  pathData?: string;
  actions?: any[];
  decorations?: any[];
}

export interface LargeFileParserOptions {
  skipLargeActions?: boolean;
  maxActions?: number;
}

function findAllPropertiesAtRoot(buffer: Uint8Array): Map<string, number> {
  const result = new Map<string, number>();
  let depth = 0;
  let inString = false;
  let escapeNext = false;
  let propertyName = '';
  let propertyNameStart = -1;

  for (let i = 0; i < buffer.length; i++) {
    const byte = buffer[i];

    if (escapeNext) {
      escapeNext = false;
      continue;
    }

    if (byte === 92) {
      escapeNext = true;
      continue;
    }

    if (byte === 34) {
      if (inString) {
        inString = false;
        if (depth === 1 && propertyNameStart !== -1) {
          propertyName = SHARED_DECODER.decode(buffer.subarray(propertyNameStart, i));
        }
      } else {
        inString = true;
        propertyNameStart = i + 1;
      }
      continue;
    }

    if (!inString) {
      if (byte === 123) {
        depth++;
      } else if (byte === 125) {
        depth--;
      } else if (byte === 58 && depth === 1 && propertyName) {
        let pos = i + 1;
        while (pos < buffer.length && (buffer[pos] === 32 || buffer[pos] === 9 || buffer[pos] === 10 || buffer[pos] === 13)) {
          pos++;
        }
        result.set(propertyName, pos);
        propertyName = '';
        propertyNameStart = -1;
      }
    }
  }

  return result;
}

function findValueEnd(buffer: Uint8Array, startPos: number): number {
  if (startPos >= buffer.length) return -1;

  const firstChar = buffer[startPos];

  if (firstChar === 34) {
    let i = startPos + 1;
    let escapeNext = false;
    while (i < buffer.length) {
      if (escapeNext) {
        escapeNext = false;
        i++;
        continue;
      }
      if (buffer[i] === 92) {
        escapeNext = true;
        i++;
        continue;
      }
      if (buffer[i] === 34) return i + 1;
      i++;
    }
    return -1;
  }

  if (firstChar === 91 || firstChar === 123) {
    const closeChar = firstChar === 91 ? 93 : 125;
    let depth = 0;
    let i = startPos;
    let inString = false;
    let escapeNext = false;

    while (i < buffer.length) {
      if (escapeNext) {
        escapeNext = false;
        i++;
        continue;
      }
      if (buffer[i] === 92) {
        escapeNext = true;
        i++;
        continue;
      }
      if (buffer[i] === 34) {
        inString = !inString;
        i++;
        continue;
      }
      if (!inString) {
        if (buffer[i] === firstChar) {
          depth++;
        } else if (buffer[i] === closeChar) {
          depth--;
          if (depth === 0) return i + 1;
        }
      }
      i++;
    }
    return -1;
  }

  let i = startPos;
  while (i < buffer.length) {
    const byte = buffer[i];
    if (byte === 44 || byte === 125 || byte === 93 || byte === 32 || byte === 9 || byte === 10 || byte === 13) {
      return i;
    }
    i++;
  }
  return i;
}

function extractValueAsString(buffer: Uint8Array, startPos: number, endPos: number): string {
  return SHARED_DECODER.decode(buffer.subarray(startPos, endPos));
}

function parseNumberArrayIncremental(
  buffer: Uint8Array,
  startPos: number,
  onProgress?: (percent: number) => void
): { values: number[]; endPos: number } | null {
  if (startPos >= buffer.length || buffer[startPos] !== 91) return null;

  const values: number[] = [];
  let i = startPos + 1;
  let depth = 1;
  let inString = false;
  let escapeNext = false;
  let lastWasComma = false;
  /** 当前数字的起始下标（-1 表示无待处理数字），替代逐字符字符串拼接 */
  let numStart = -1;
  const totalLength = buffer.length;

  const flushNumber = (end: number) => {
    if (numStart !== -1 && !lastWasComma) {
      const num = Number(SHARED_DECODER.decode(buffer.subarray(numStart, end)));
      if (!isNaN(num)) values.push(num);
    }
    numStart = -1;
  };

  while (i < buffer.length) {
    const byte = buffer[i];

    if (escapeNext) {
      escapeNext = false;
      i++;
      continue;
    }

    if (byte === 92) {
      escapeNext = true;
      i++;
      continue;
    }

    if (byte === 34) {
      inString = !inString;
      i++;
      continue;
    }

    if (!inString) {
      if (byte === 91) {
        depth++;
        i++;
        lastWasComma = false;
      } else if (byte === 93) {
        depth--;
        if (depth === 0) {
          flushNumber(i);
          return { values, endPos: i + 1 };
        }
        i++;
        lastWasComma = false;
      } else if (byte === 44) {
        flushNumber(i);
        lastWasComma = true;
        i++;
      } else if ((byte >= 48 && byte <= 57) || byte === 45 || byte === 46) {
        if (numStart === -1) numStart = i;
        lastWasComma = false;
        i++;
      } else if (byte === 32 || byte === 9 || byte === 10 || byte === 13) {
        i++;
      } else {
        i++;
      }
    } else {
      i++;
    }

    if (onProgress && i % 5000000 === 0) {
      onProgress(Math.round((i / totalLength) * 100));
    }
  }

  return { values, endPos: i };
}

function parseObjectArrayIncremental(
  buffer: Uint8Array,
  startPos: number,
  onProgress?: (percent: number) => void,
  maxObjects?: number
): { values: any[]; endPos: number } | null {
  if (startPos >= buffer.length || buffer[startPos] !== 91) return null;

  const values: any[] = [];
  let i = startPos + 1;
  let depth = 1;
  let inString = false;
  let escapeNext = false;
  let objectStart = -1;
  const totalLength = buffer.length;
  let objectCount = 0;

  while (i < buffer.length && (buffer[i] === 32 || buffer[i] === 9 || buffer[i] === 10 || buffer[i] === 13)) {
    i++;
  }

  if (buffer[i] === 93) {
    return { values: [], endPos: i + 1 };
  }

  while (i < buffer.length) {
    const byte = buffer[i];

    if (escapeNext) {
      escapeNext = false;
      i++;
      continue;
    }

    if (byte === 92) {
      escapeNext = true;
      i++;
      continue;
    }

    if (byte === 34) {
      inString = !inString;
      i++;
      continue;
    }

    if (!inString) {
      if (byte === 123) {
        if (depth === 1 && objectStart === -1) objectStart = i;
        depth++;
        i++;
      } else if (byte === 125) {
        depth--;
        if (depth === 1 && objectStart !== -1) {
          try {
            const obj = JSON.parse(extractValueAsString(buffer, objectStart, i + 1));
            values.push(obj);
            objectCount++;

            if (maxObjects && objectCount >= maxObjects) {
              let searchPos = i + 1;
              while (searchPos < buffer.length && buffer[searchPos] !== 93) searchPos++;
              return { values, endPos: searchPos + 1 };
            }
          } catch (e) { /* skip malformed objects */ }
          objectStart = -1;

          if (onProgress && objectCount % 50000 === 0) {
            onProgress(Math.round((i / totalLength) * 100));
          }
        }
        i++;
      } else if (byte === 91) {
        depth++;
        i++;
      } else if (byte === 93) {
        depth--;
        if (depth === 0) return { values, endPos: i + 1 };
        i++;
      } else {
        i++;
      }
    } else {
      i++;
    }
  }

  return { values, endPos: i };
}

export class LargeFileParser {
  private onProgress?: (stage: string, percent: number) => void;
  private skipLargeActions: boolean = false;
  private maxActions: number = 0;

  constructor(
    onProgress?: (stage: string, percent: number) => void,
    options?: LargeFileParserOptions
  ) {
    this.onProgress = onProgress;
    if (options) {
      this.skipLargeActions = options.skipLargeActions ?? false;
      this.maxActions = options.maxActions ?? 0;
    }
  }

  parse(input: ArrayBuffer): LargeFileParseResult {
    let view = new Uint8Array(input);

    if (view.length >= 3 && view[0] === BOM[0] && view[1] === BOM[1] && view[2] === BOM[2]) {
      view = view.subarray(3);
    }

    this.onProgress?.('scanning', 5);

    const properties = findAllPropertiesAtRoot(view);

    const angleDataPos = properties.get('angleData') ?? -1;
    const pathDataPos = properties.get('pathData') ?? -1;
    const settingsPos = properties.get('settings') ?? -1;
    const actionsPos = properties.get('actions') ?? -1;
    const decorationsPos = properties.get('decorations') ?? -1;

    const result: LargeFileParseResult = {};

    if (settingsPos !== -1) {
      this.onProgress?.('parsing_settings', 10);
      const settingsEnd = findValueEnd(view, settingsPos);
      if (settingsEnd !== -1) {
        try {
          result.settings = JSON.parse(extractValueAsString(view, settingsPos, settingsEnd));
        } catch (e) {
          result.settings = {};
        }
      }
    }

    if (angleDataPos !== -1) {
      this.onProgress?.('parsing_angleData', 15);
      const angleResult = parseNumberArrayIncremental(view, angleDataPos, (p) => {
        this.onProgress?.('parsing_angleData', 15 + p * 0.25);
      });
      if (angleResult) {
        result.angleData = angleResult.values;
      }
    }

    if (pathDataPos !== -1) {
      const pathEnd = findValueEnd(view, pathDataPos);
      if (pathEnd !== -1) {
        const pathStr = extractValueAsString(view, pathDataPos, pathEnd);
        result.pathData = pathStr.slice(1, -1);
      }
    }

    if (actionsPos !== -1) {
      const actionsEnd = findValueEnd(view, actionsPos);
      const actionsSize = actionsEnd - actionsPos;

      this.onProgress?.('parsing_actions', 50);

      if (actionsSize > 100 * 1024 * 1024 && this.skipLargeActions) {
        result.actions = [];
      } else if (actionsSize > 50 * 1024 * 1024) {
        const actionsResult = parseObjectArrayIncremental(
          view,
          actionsPos,
          (p) => this.onProgress?.('parsing_actions', 50 + p * 0.45),
          this.maxActions || undefined
        );
        if (actionsResult) result.actions = actionsResult.values;
      } else {
        try {
          result.actions = JSON.parse(extractValueAsString(view, actionsPos, actionsEnd));
        } catch (e) {
          result.actions = [];
        }
      }
    }

    if (decorationsPos !== -1) {
      this.onProgress?.('parsing_decorations', 95);
      const decorationsEnd = findValueEnd(view, decorationsPos);
      if (decorationsEnd !== -1) {
        try {
          result.decorations = JSON.parse(extractValueAsString(view, decorationsPos, decorationsEnd));
        } catch (e) {
          result.decorations = [];
        }
      }
    }

    this.onProgress?.('complete', 100);
    return result;
  }

  stringify(obj: any): string {
    return JSON.stringify(obj);
  }
}

export default LargeFileParser;
