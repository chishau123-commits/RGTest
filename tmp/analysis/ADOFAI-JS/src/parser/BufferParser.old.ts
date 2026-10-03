import Parser from "./Parser";
import StringParser from "./StringParser";

let BOM: Buffer;
let COMMA: Buffer;
let BufferAvailable = true;

try {
  BOM = Buffer.of(0xef, 0xbb, 0xbf);
  COMMA = Buffer.from(",");
} catch (e) {
  BufferAvailable = false;
  console.warn('Buffer is not available in current environment, try to use ArrayBufferParser');
  // minimal shims to keep types happy; won't be used when BufferUnavailable
  BOM = { equals: () => false, subarray: () => null } as any;
  COMMA = { equals: () => false, subarray: () => null } as any;
}

/**
 * Inspect buffer to detect an unescaped raw newline inside a quoted string.
 * If found, we must fallback to the more tolerant StringParser.
 */
function hasRawNewlineInStringBuffer(buf: Uint8Array): boolean {
  let last: "other" | "string" | "escape" | "comma" = "other";
  for (let i = 0; i < buf.length; i++) {
    const c = buf[i];
    if (last === "escape") {
      last = "string";
      continue;
    }
    switch (c) {
      case 34: // "
        if (last === "string") {
          last = "other";
        } else {
          if (last === "comma") {
            // entering string after comma
          }
          last = "string";
        }
        break;
      case 92: // \
        if (last === "string") last = "escape";
        break;
      case 44: // ,
        if (last === "other") last = "comma";
        break;
      case 93: // ]
      case 125: // }
        if (last === "comma") last = "other";
        break;
      // whitespace bytes
      case 9:
      case 10:
      case 11:
      case 12:
      case 13:
      case 32:
        // If we see newline (10) or carriage (13) while inside a string AND not escaped,
        // that's a raw newline in string -> not valid strict JSON; detect and return true.
        if ((c === 10 || c === 13) && last === "string") {
          return true;
        }
        break;
      default:
        if (last === "comma") {
          last = "other";
        }
        break;
    }
  }
  return false;
}

/**
 * Normalize buffer by removing insignificant whitespace outside strings.
 * Works on Uint8Array and returns Buffer (for node) or Uint8Array (for arraybuffer flow).
 * This function keeps contents inside strings untouched.
 */
function normalizeJsonUint8(buf: Uint8Array): Uint8Array {
  const builder: Uint8Array[] = [];
  let last: "other" | "string" | "escape" | "comma" = "other";
  let from = 0;
  for (let i = 0; i < buf.length; i++) {
    const charCode = buf[i];
    if (last === "escape") {
      last = "string";
      continue;
    } else {
      switch (charCode) {
        case 34: // "
          switch (last) {
            case "string":
              last = "other";
              break;
            case "comma":
              // when we hit a quote after a comma we keep the comma boundary
              builder.push(COMMA ? (COMMA as any) : new Uint8Array([44]) as any);
            default:
              last = "string";
              break;
          }
          break;
        case 92: // \
          if (last === "string") last = "escape";
          break;
        case 44: // ,
          builder.push(buf.subarray(from, i));
          from = i + 1;
          if (last === "other") last = "comma";
          break;
        case 93:
        case 125:
          if (last === "comma") last = "other";
          break;
        case 9:
        case 10:
        case 11:
        case 12:
        case 13:
        case 32:
          // ignore whitespace outside strings
          break;
        default:
          if (last === "comma") {
            builder.push(COMMA ? (COMMA as any) : new Uint8Array([44]) as any);
            last = "other";
          }
          break;
      }
    }
  }
  builder.push(buf.subarray(from));
  // concat
  let total = 0;
  for (const piece of builder) total += piece.length;
  const out = new Uint8Array(total);
  let offset = 0;
  for (const piece of builder) {
    out.set(piece, offset);
    offset += piece.length;
  }
  return out;
}

export class BufferParser extends Parser<Buffer | string, any> {
  parse(input: Buffer | string): any {
    // string input: delegate to StringParser (keeps current behavior)
    if (typeof input === "string") {
      return StringParser.prototype.parse.call(StringParser.prototype, input);
    }

    // Node Buffer path
    const nodeBuf = input as Buffer;
    // strip BOM quickly
    const stripped = stripBOM(nodeBuf);
    const u8 = new Uint8Array(stripped);

    // If there is any raw newline inside a quoted string, fallback to StringParser
    if (hasRawNewlineInStringBuffer(u8)) {
      // decode exactly and let StringParser tolerant-parse it
      return StringParser.prototype.parse.call(StringParser.prototype, decodeStringFromUTF8BOM(stripped));
    }

    // Fast path: normalize then try JSON.parse (much faster than StringParser).
    try {
      const normalized = normalizeJsonUint8(u8);
      const text = decodeStringFromUTF8BOM(Buffer.from(normalized));
      return JSON.parse(text);
    } catch (e) {
      // Fallback to StringParser if anything unexpected occurs
      return StringParser.prototype.parse.call(StringParser.prototype, decodeStringFromUTF8BOM(stripped));
    }
  }

  stringify(obj: any): string {
    return JSON.stringify(obj);
  }
}

export function stripBOM(buffer: Buffer): Buffer {
  if (buffer.length >= 3 && BOM.equals(buffer.subarray(0, 3))) {
    return buffer.subarray(3)
  }
  return buffer
}

export function normalizeJsonBuffer(text: Buffer): Buffer {
  // keep backward compatibility: use the Uint8 normalizer and return a Buffer
  const u8 = normalizeJsonUint8(new Uint8Array(text));
  return Buffer.from(u8);
}

export function decodeStringFromUTF8BOM(buffer: Buffer): string {
  return stripBOM(buffer).toString("utf-8")
}

export default BufferParser;
