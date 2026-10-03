import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, BgDisplayMode, BgShapeType, ABoolean } from '../types';

export interface CustomBackground extends AdofaiEvent {
    eventType: 'CustomBackground';
    bgImage?: string;
    bgImageColor?: string;
    parallax?: Vec2;
    bgDisplayMode?: BgDisplayMode;
    lockRot?: ABoolean;
    loopBG?: ABoolean;
    scalingRatio?: number;
    imageSmoothing?: ABoolean;
    showDefaultBGIfNoImage?: ABoolean;
    showDefaultBGTile?: ABoolean;
    defaultBGTileColor?: string;
    defaultBGShapeType?: BgShapeType;
    defaultBGShapeColor?: string;
    duration?: number;
    easing?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    angleOffset?: number;
}
