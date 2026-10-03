import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface AnimateTrack extends AdofaiEvent {
    eventType: 'AnimateTrack';
    startTile?: number;
    floorCount?: number;
    duration?: number;
    easing?: string;
    tag?: string;
    positionOffset?: Vec2;
    rotation?: number;
    scale?: Vec2;
    opacity?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
}
