import StringParser from './StringParser';
import { LevelOptions, ParseProvider } from '../structure';

class BaseParser {
    static parseError(f: string) {
        return f;
    }

    /**
        * @param {string} t - Input Content
        * @param {object} provider - ParserProvider
        * @returns {LevelOptions} ADOFAI File Object
    */
    static parseAsObject(t: string, provider?: ParseProvider): LevelOptions {
        const parser = provider || JSON;
        return parser.parse(BaseParser.parseAsText(t)) as LevelOptions;
    }

    /**
        * @param {string} t - Input Content
        * @returns {string} ADOFAI File Content
    */
    static parseAsText(t: string) {
        return this.parseError(t);
    }
}

export default BaseParser;