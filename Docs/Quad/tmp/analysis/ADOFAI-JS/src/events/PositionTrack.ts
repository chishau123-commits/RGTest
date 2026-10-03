import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, ABoolean } from '../types';

export interface PositionTrack extends AdofaiEvent {
    eventType: 'PositionTrack';
    startTile?: number;
    floorCount?: number;
    positionOffset?: Vec2;
    easing?: string;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
