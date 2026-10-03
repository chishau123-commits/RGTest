import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface MoveTrack extends AdofaiEvent {
    eventType: 'MoveTrack';
    startTile?: number;
    floorCount?: number;
    duration?: number;
    positionOffset?: Vec2;
    rotation?: number;
    scale?: Vec2;
    opacity?: number;
    easing?: string;
    tag?: string;
    angleOffset?: number;
}
