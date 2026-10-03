import type { AdofaiEvent } from '../structure/interfaces';

export interface Hold extends AdofaiEvent {
    eventType: 'Hold';
    duration?: number;
    angleOffset?: number;
}
