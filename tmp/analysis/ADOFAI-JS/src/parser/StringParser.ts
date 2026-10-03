import Parser from "./Parser";

class StringParser extends Parser<string, any> {
    parse(text: string | null, reviver?: (key: string, value: any) => any): any {
        if (text == null) return null;
        const result = new ParserX(text).parseValue();
        if (typeof reviver === "function") {
            return StringParser._applyReviver("", result, reviver);
        }
        return result;
    }

    stringify(value: any, replacer?: (key: string, value: any) => any, space?: string | number): string {
        const serializer = new Serializer(replacer, space);
        return serializer.serialize(value);
    }

    static _applyReviver(key: string, value: any, reviver: (key: string, value: any) => any): any {
        if (value && typeof value === "object") {
            if (Array.isArray(value)) {
                for (let i = 0; i < value.length; i++) {
                    value[i] = StringParser._applyReviver(i.toString(), value[i], reviver);
                }
            } else {
                for (const prop in value) {
                    if (Object.prototype.hasOwnProperty.call(value, prop)) {
                        value[prop] = StringParser._applyReviver(prop, value[prop], reviver);
                    }
                }
            }
        }
        return reviver(key, value);
    }
}

const CHAR_BACKSPACE = 8;
const CHAR_TAB = 9;
const CHAR_LF = 10;
const CHAR_FF = 12;
const CHAR_CR = 13;
const CHAR_QUOTE = 34;
const CHAR_COMMA = 44;
const CHAR_COLON = 58;
const CHAR_SQUARE_OPEN = 91;
const CHAR_SQUARE_CLOSE = 93;
const CHAR_CURLY_OPEN = 123;
const CHAR_CURLY_CLOSE = 125;
const CHAR_BACKSLASH = 92;
const CHAR_SPACE = 32;
const CHAR_BOM = 0xfeff;

/** charCode → escaped replacement for the characters JSON defines escapes for */
const ESCAPE_BY_CODE: Record<number, string> = {
    [CHAR_QUOTE]: '\\"',
    [CHAR_BACKSLASH]: "\\\\",
    [47]: "/",
    [98]: "\\b",
    [102]: "\\f",
    [110]: "\\n",
    [114]: "\\r",
    [116]: "\\t"
};

function hexValue(code: number): number {
    if (code >= 48 && code <= 57) return code - 48;          // 0-9
    if (code >= 97 && code <= 102) return code - 87;         // a-f
    if (code >= 65 && code <= 70) return code - 55;          // A-F
    return -1;
}

/**
 * Unescape a raw substring (between quotes) of a JSON document.
 * Replicates the original per-character semantics:
 * - known single-char escapes are mapped, unknown escapes are dropped
 * - \uXXXX uses prefix parsing like Number.parseInt(x, 16); zero valid
 *   digits yield U+0000 (fromCharCode(NaN))
 * - a trailing lone backslash at EOF is dropped
 */
function unescapeJsonRange(json: string, start: number, end: number): string {
    let result = "";
    let chunkStart = start;
    let i = start;

    while (i < end) {
        if (json.charCodeAt(i) !== CHAR_BACKSLASH) {
            i++;
            continue;
        }

        // backslash at end of range: unterminated escape (EOF), drop it
        if (i + 1 >= end) break;

        const escapedCode = json.charCodeAt(i + 1);

        if (escapedCode === 117) { // 'u'
            if (chunkStart < i) result += json.slice(chunkStart, i);
            let value = NaN as number;
            let digits = 0;
            let j = i + 2;
            while (j < end && digits < 4) {
                const d = hexValue(json.charCodeAt(j));
                if (d === -1) break;
                value = digits === 0 ? d : value * 16 + d;
                digits++;
                j++;
            }
            result += String.fromCharCode(value);
            i = j;
            chunkStart = i;
        } else {
            if (chunkStart < i) result += json.slice(chunkStart, i);
            const replacement = ESCAPE_BY_CODE[escapedCode];
            if (replacement !== undefined) result += replacement;
            i += 2;
            chunkStart = i;
        }
    }

    if (chunkStart < end) result += json.slice(chunkStart, end);
    return result;
}

class ParserX {
    static WHITE_SPACE = " \t\n\r\uFEFF";
    static WORD_BREAK = ' \t\n\r{}[],:"';
    static TOKEN = {
        NONE: 0,
        CURLY_OPEN: 1,
        CURLY_CLOSE: 2,
        SQUARED_OPEN: 3,
        SQUARED_CLOSE: 4,
        COLON: 5,
        COMMA: 6,
        STRING: 7,
        NUMBER: 8,
        TRUE: 9,
        FALSE: 10,
        NULL: 11,
    };
    private json: string;
    private position: number;
    private endSection: string | null;
    constructor(jsonString: string, endSection: string | null = null) {
        this.json = jsonString;
        this.position = 0;
        this.endSection = endSection;
        if (this.position < this.json.length && this.json.charCodeAt(0) === CHAR_BOM) {
            this.position++;
        }
    }
    parseValue(): any {
        return this.parseByToken(this.nextToken);
    }
    parseObject(): Record<string, any> | null {
        const obj: Record<string, any> = {};
        this.read();
        while (true) {
            let nextToken;
            do {
                nextToken = this.nextToken;
                if (nextToken === ParserX.TOKEN.NONE) {
                    return null;
                }
                if (nextToken === ParserX.TOKEN.CURLY_CLOSE) {
                    return obj;
                }
            } while (nextToken === ParserX.TOKEN.COMMA);
            const key = this.parseString();
            if (key === null) {
                return null;
            }
            if (this.nextToken !== ParserX.TOKEN.COLON) {
                return null;
            }
            if (this.endSection == null || key !== this.endSection) {
                this.read();
                obj[key] = this.parseValue();
            } else {
                return obj;
            }
        }
    }
    parseArray(): any[] | null {
        const array: any[] = [];
        this.read();
        let parsing = true;
        while (parsing) {
            const nextToken = this.nextToken;
            switch (nextToken) {
                case ParserX.TOKEN.NONE:
                    return null;
                case ParserX.TOKEN.SQUARED_CLOSE:
                    parsing = false;
                    break;
                case ParserX.TOKEN.COMMA:
                    break;
                default:
                    const value = this.parseByToken(nextToken);
                    array.push(value);
                    break;
            }
        }
        return array;
    }
    parseByToken(token: number): any {
        switch (token) {
            case ParserX.TOKEN.CURLY_OPEN:
                return this.parseObject();
            case ParserX.TOKEN.SQUARED_OPEN:
                return this.parseArray();
            case ParserX.TOKEN.STRING:
                return this.parseString();
            case ParserX.TOKEN.NUMBER:
                return this.parseNumber();
            case ParserX.TOKEN.TRUE:
                return true;
            case ParserX.TOKEN.FALSE:
                return false;
            case ParserX.TOKEN.NULL:
                return null;
            default:
                return null;
        }
    }
    parseString(): string | null {
        this.read(); // consume opening quote
        const json = this.json;
        const len = json.length;
        const start = this.position;
        let pos = start;
        let hasEscape = false;

        while (pos < len) {
            const c = json.charCodeAt(pos);
            if (c === CHAR_QUOTE) break;
            if (c === CHAR_BACKSLASH) {
                hasEscape = true;
                pos += 2;
            } else {
                pos++;
            }
        }

        const end = pos < len ? pos : len;
        this.position = Math.min(end + 1, len);

        if (!hasEscape) {
            return json.slice(start, end);
        }
        return unescapeJsonRange(json, start, end);
    }
    parseNumber(): number {
        const word = this.nextWord;
        if (word.indexOf(".") === -1) {
            return Number.parseInt(word, 10) || 0;
        } else {
            return Number.parseFloat(word) || 0.0;
        }
    }
    eatWhitespace(): void {
        const json = this.json;
        const len = json.length;
        let pos = this.position;
        while (pos < len) {
            const c = json.charCodeAt(pos);
            if (
                c === CHAR_SPACE || c === CHAR_TAB || c === CHAR_LF || c === CHAR_CR ||
                c === 11 /* \v */ || c === 12 /* \f */ || c === CHAR_BOM
            ) {
                pos++;
            } else {
                break;
            }
        }
        this.position = pos;
    }
    peek(): number {
        if (this.position >= this.json.length) {
            return -1;
        }
        return this.json.charCodeAt(this.position);
    }
    read(): number {
        if (this.position >= this.json.length) {
            return -1;
        }
        return this.json.charCodeAt(this.position++);
    }
    get peekChar(): string {
        const code = this.peek();
        return code === -1 ? "\0" : String.fromCharCode(code);
    }
    get nextChar(): string {
        const code = this.read();
        return code === -1 ? "\0" : String.fromCharCode(code);
    }
    get nextWord(): string {
        const json = this.json;
        const len = json.length;
        const start = this.position;
        let pos = start;
        while (pos < len) {
            const c = json.charCodeAt(pos);
            if (
                c === CHAR_SPACE || c === CHAR_TAB || c === CHAR_LF || c === CHAR_CR ||
                c === CHAR_CURLY_OPEN || c === CHAR_CURLY_CLOSE ||
                c === CHAR_SQUARE_OPEN || c === CHAR_SQUARE_CLOSE ||
                c === CHAR_COMMA || c === CHAR_COLON || c === CHAR_QUOTE
            ) {
                break;
            }
            pos++;
        }
        this.position = pos;
        return json.slice(start, pos);
    }
    get nextToken(): number {
        this.eatWhitespace();
        if (this.peek() === -1) {
            return ParserX.TOKEN.NONE;
        }
        const code = this.peek();
        switch (code) {
            case CHAR_QUOTE:
                return ParserX.TOKEN.STRING;
            case CHAR_COMMA:
                this.read();
                return ParserX.TOKEN.COMMA;
            case 45: // -
            case 48: case 49: case 50: case 51: case 52:
            case 53: case 54: case 55: case 56: case 57:
                return ParserX.TOKEN.NUMBER;
            case CHAR_COLON:
                return ParserX.TOKEN.COLON;
            case CHAR_SQUARE_OPEN:
                return ParserX.TOKEN.SQUARED_OPEN;
            case CHAR_SQUARE_CLOSE:
                this.read();
                return ParserX.TOKEN.SQUARED_CLOSE;
            case CHAR_CURLY_OPEN:
                return ParserX.TOKEN.CURLY_OPEN;
            case CHAR_CURLY_CLOSE:
                this.read();
                return ParserX.TOKEN.CURLY_CLOSE;
            default: {
                const word = this.nextWord;
                switch (word) {
                    case "false":
                        return ParserX.TOKEN.FALSE;
                    case "true":
                        return ParserX.TOKEN.TRUE;
                    case "null":
                        return ParserX.TOKEN.NULL;
                    default:
                        return ParserX.TOKEN.NONE;
                }
            }
        }
    }
}

class Serializer {
    private result: string = "";
    private replacer: ((key: string, value: any) => any) | null;
    private space: string | number | null;
    private indent: number = 0;
    private indentStr: string = "";
    private indentPrefix: string = "";
    constructor(replacer?: (key: string, value: any) => any, space?: string | number) {
        this.replacer = replacer || null;
        this.space = space || null;
        if (typeof space === "number") {
            this.indentStr = " ".repeat(Math.min(10, Math.max(0, space)));
        } else if (typeof space === "string") {
            this.indentStr = space.slice(0, 10);
        }
    }
    serialize(obj: any): string {
        this.result = "";
        this.indent = 0;
        this.indentPrefix = "";
        this.serializeValue(obj, "");
        return this.result;
    }
    private pushIndent(delta: number): void {
        this.indent += delta;
        if (this.indentStr) {
            this.indentPrefix = this.indentStr.repeat(this.indent);
        }
    }
    private serializeValue(value: any, key: string = ""): void {
        if (typeof this.replacer === "function") {
            value = this.replacer(key, value);
        }
        if (value === null || value === undefined) {
            this.result += "null";
        } else if (typeof value === "string") {
            this.serializeString(value);
        } else if (typeof value === "boolean") {
            this.result += value ? "true" : "false";
        } else if (Array.isArray(value)) {
            this.serializeArray(value);
        } else if (typeof value === "object") {
            this.serializeObject(value);
        } else {
            this.serializeOther(value);
        }
    }
    private serializeObject(obj: Record<string, any>): void {
        let first = true;
        this.result += "{";
        if (this.indentStr) {
            this.result += "\n";
            this.pushIndent(1);
        }
        const keys = Object.keys(obj);
        for (let k = 0; k < keys.length; k++) {
            const key = keys[k];
            if (Array.isArray(this.replacer) && !this.replacer.includes(key)) {
                continue;
            }
            if (!first) {
                this.result += ",";
                if (this.indentStr) this.result += "\n";
            }
            if (this.indentStr) {
                this.result += this.indentPrefix;
            }
            this.serializeString(key.toString());
            this.result += ":";
            if (this.indentStr) this.result += " ";
            this.serializeValue(obj[key], key);
            first = false;
        }
        if (this.indentStr) {
            this.result += "\n";
            this.pushIndent(-1);
            this.result += this.indentPrefix;
        }
        this.result += "}";
    }
    private serializeArray(array: any[]): void {
        this.result += "[";
        if (this.indentStr && array.length > 0) {
            this.result += "\n";
            this.pushIndent(1);
        }
        let first = true;
        for (let i = 0; i < array.length; i++) {
            if (!first) {
                this.result += ",";
                if (this.indentStr) this.result += "\n";
            }
            if (this.indentStr) {
                this.result += this.indentPrefix;
            }
            this.serializeValue(array[i], i.toString());
            first = false;
        }
        if (this.indentStr && array.length > 0) {
            this.result += "\n";
            this.pushIndent(-1);
            this.result += this.indentPrefix;
        }
        this.result += "]";
    }
    private serializeString(str: string): void {
        const len = str.length;

        // Fast path: nothing to escape.
        let needsEscape = false;
        for (let i = 0; i < len; i++) {
            const code = str.charCodeAt(i);
            if (
                code === CHAR_QUOTE || code === CHAR_BACKSLASH ||
                code < CHAR_SPACE || code > 126
            ) {
                needsEscape = true;
                break;
            }
        }
        if (!needsEscape) {
            this.result += '"';
            this.result += str;
            this.result += '"';
            return;
        }

        // Slow path: copy safe runs in bulk, emit escapes one by one.
        this.result += '"';
        let chunkStart = 0;
        for (let i = 0; i < len; i++) {
            const code = str.charCodeAt(i);
            let escaped: string | undefined;
            if (code < CHAR_SPACE) {
                switch (code) {
                    case CHAR_BACKSPACE: escaped = "\\b"; break;
                    case CHAR_TAB: escaped = "\\t"; break;
                    case CHAR_LF: escaped = "\\n"; break;
                    case CHAR_FF: escaped = "\\f"; break;
                    case CHAR_CR: escaped = "\\r"; break;
                    default: escaped = "\\u" + code.toString(16).padStart(4, "0"); break;
                }
            } else if (code === CHAR_QUOTE) {
                escaped = '\\"';
            } else if (code === CHAR_BACKSLASH) {
                escaped = "\\\\";
            } else if (code > 126) {
                escaped = "\\u" + code.toString(16).padStart(4, "0");
            }

            if (escaped !== undefined) {
                if (chunkStart < i) this.result += str.slice(chunkStart, i);
                this.result += escaped;
                chunkStart = i + 1;
            }
        }
        if (chunkStart < len) this.result += str.slice(chunkStart, len);
        this.result += '"';
    }
    private serializeOther(value: any): void {
        if (typeof value === "number") {
            if (isFinite(value)) {
                this.result += value.toString();
            } else {
                this.result += "null";
            }
        } else {
            this.serializeString(value.toString());
        }
    }
}

export default StringParser;
export { ParserX, Serializer };
