import Parser from "./Parser";
import StringParser, { ParserX } from "./StringParser";

/** 共享解码器：整体解码一次远快于逐字符串解码 */
const DECODER = new TextDecoder("utf-8");

/**
 * 字节流 JSON 解析器
 * 输入为字节流时先整体解码一次（原生实现，速度极快），
 * 再交给优化后的字符串解析引擎，避免在热路径上做数十万次小型解码与分配。
 * 解析器本身是宽容的：尾随/重复逗号、字符串内原始换行均可处理。
 */
export class BufferParser extends Parser<Uint8Array | Buffer | string, any> {
  parse(input: Uint8Array | Buffer | string): any {
    if (typeof input === "string") {
      return new StringParser().parse(input);
    }

    const u8 = input instanceof Uint8Array ? input : new Uint8Array(input);
    // TextDecoder 默认会剥离 UTF-8 BOM
    return new ParserX(DECODER.decode(u8)).parseValue();
  }

  stringify(obj: any): string {
    return JSON.stringify(obj);
  }
}

export function stripBOM(buffer: Uint8Array | Buffer): Uint8Array {
  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    return buffer.subarray(3);
  }
  return buffer;
}

export default BufferParser;
