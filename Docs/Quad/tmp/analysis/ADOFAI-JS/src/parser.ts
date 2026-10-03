import Parser from "./parser/index";
import BufferParser from './parser/BufferParser';
import ArrayBufferParser from "./parser/ArrayBufferParser";
import StringParser from "./parser/StringParser";
import LargeFileParser from "./parser/LargeFileParser";

export {StringParser, BufferParser, ArrayBufferParser, LargeFileParser};
export default Parser;
