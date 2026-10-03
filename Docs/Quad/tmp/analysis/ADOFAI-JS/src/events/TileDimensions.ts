import type { AdofaiEvent } from '../structure/interfaces';

export interface TileDimensions extends AdofaiEvent {
    eventType: 'TileDimensions';
    scale?: number;
    scaleTo?: number;
    scaleToDuration?: number;
    scaleToEasing?: string;
    angleOffset?: number;
}
