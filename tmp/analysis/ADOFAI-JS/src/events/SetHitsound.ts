import type { AdofaiEvent } from '../structure/interfaces';
import type { HitsoundType, ABoolean } from '../types';

export interface SetHitsound extends AdofaiEvent {
    eventType: 'SetHitsound';
    hitsound?: HitsoundType;
    hitsoundVolume?: number;
    customHitsound?: string;
    ease?: ABoolean;
    angleOffset?: number;
}
