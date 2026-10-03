import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface ChangeTrack extends AdofaiEvent {
    eventType: 'ChangeTrack';
    startTile?: number;
    floorCount?: number;
    duration?: number;
    positionOffset?: Vec2;
    angleOffset?: number;
    easing?: string;
    tag?: string;
}
