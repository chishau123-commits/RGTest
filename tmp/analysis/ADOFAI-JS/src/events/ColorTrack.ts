import type { AdofaiEvent } from '../structure/interfaces';

export interface ColorTrack extends AdofaiEvent {
    eventType: 'ColorTrack';
    startTile?: number;
    floorCount?: number;
    duration?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    easing?: string;
    tag?: string;
}
