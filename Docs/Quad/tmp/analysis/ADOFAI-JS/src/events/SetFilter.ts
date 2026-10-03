import type { AdofaiEvent } from '../structure/interfaces';
import type { FilterType } from '../types';

export interface SetFilter extends AdofaiEvent {
    eventType: 'SetFilter';
    filterType?: FilterType;
    intensity?: number;
    duration?: number;
    easing?: string;
    angleOffset?: number;
    [key: string]: any;
}
