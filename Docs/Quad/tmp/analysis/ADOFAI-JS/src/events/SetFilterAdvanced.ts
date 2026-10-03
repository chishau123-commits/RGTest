import type { AdofaiEvent } from '../structure/interfaces';
import type { FilterType } from '../types';

export interface SetFilterAdvanced extends AdofaiEvent {
    eventType: 'SetFilterAdvanced';
    filterType?: FilterType;
    intensity?: number;
    duration?: number;
    easing?: string;
    angleOffset?: number;
    [key: string]: any;
}
