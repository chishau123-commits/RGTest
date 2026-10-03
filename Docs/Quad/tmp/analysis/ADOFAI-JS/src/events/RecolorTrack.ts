import type { AdofaiEvent } from '../structure/interfaces';

export interface RecolorTrack extends AdofaiEvent {
    eventType: 'RecolorTrack';
    startTile?: number;
    floorCount?: number;
    duration?: number;
    trackColor?: string;
    trackColorTo?: string;
    trackColorToDuration?: number;
    trackColorToEasing?: string;
    easing?: string;
    tag?: string;
    angleOffset?: number;
}
