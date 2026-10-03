import type { AdofaiEvent } from '../structure/interfaces';

export interface ScreenTile extends AdofaiEvent {
    eventType: 'ScreenTile';
    duration?: number;
    tileX?: number;
    tileY?: number;
    easing?: string;
    angleOffset?: number;
}
