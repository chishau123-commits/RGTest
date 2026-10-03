abstract class Parser<TInput = any, TOutput = any> {
    abstract parse(input: TInput): TOutput;
    abstract stringify(obj: TOutput): TInput;
}

export default Parser;