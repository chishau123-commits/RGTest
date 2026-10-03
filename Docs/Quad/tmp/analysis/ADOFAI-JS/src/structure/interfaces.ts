export interface AdofaiEvent {
    floor: number;
    eventType: string;
    [key: string]: any;
}

export interface ActionData {
    eventType: string;
    [key: string]: any;
}

export interface LevelOptions {
    pathData?: string;
    angleData?: number[];
    actions: AdofaiEvent[];
    settings: Record<string, any>;
    decorations: AdofaiEvent[];
    [key: string]: any;
}

export interface EventCallback {
    guid: string;
    callback: Function;
}

export interface GuidCallback {
    eventName: string;
    callback: Function;
}

export interface Tile {
    direction?: number;
    angle?: number;
    actions: ActionData[];
    addDecorations?: ActionData[];
    _lastdir?: number;
    twirl?: number;
    position?: number[];
    extraProps?: Record<string, any>;
}

export interface ParseProvider {
    parse(t: string): LevelOptions;
}

export interface ParseProgressEvent {
    stage: 'start' | 'pathData' | 'angleData' | 'relativeAngle' | 'tilePosition' | 'complete';
    current: number;
    total: number;
    percent: number;
    /** 当前阶段产生的数据 */
    data?: {
        /** pathData 阶段: 原始 pathData 字符串; angleData 阶段: 解析后的角度数组 */
        source?: string | number[];
        /** 已处理的部分数据 */
        processed?: number[];
        /** 当前处理的 tile 数据 */
        tile?: Tile;
        /** 当前处理的 tile 索引 */
        tileIndex?: number;
        /** angleData: 当前角度值 */
        angle?: number;
        /** relativeAngle: 计算出的相对角度 */
        relativeAngle?: number;
        /** tilePosition: 当前坐标 */
        position?: number[];
    };
}

export interface PrecomputedProgressEvents {
    start: ParseProgressEvent[];
    pathData: ParseProgressEvent[];
    angleData: ParseProgressEvent[];
    relativeAngle: ParseProgressEvent[];
    tilePosition: ParseProgressEvent[];
    complete: ParseProgressEvent[];
}

/**
 * 轻量级渲染数据 - 只包含渲染必需的数据，不包含完整的 tile 对象
 * 用于大物量谱面，避免内存爆炸
 */
export interface LightweightRenderData {
    /** 索引 */
    index: number;
    /** 相对角度（渲染用） */
    angle: number;
    /** 坐标 [x, y] */
    position: [number, number];
    /** 是否有 Twirl */
    hasTwirl: boolean;
}

/**
 * 轻量级预计算结果 - 用于渲染器逐帧渲染
 */
export interface LightweightPrecomputedData {
    /** 总 tile 数量 */
    totalTiles: number;
    /** 相对角度数组（索引对应 tile 索引） */
    angles: number[];
    /** 坐标数组（索引对应 tile 索引） */
    positions: [number, number][];
    /** 是否有 Twirl 的标记数组 */
    twirlFlags: boolean[];
    /** 是否已完成预计算 */
    computed: boolean;
}