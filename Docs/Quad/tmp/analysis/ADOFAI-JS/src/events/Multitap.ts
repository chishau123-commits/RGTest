import type { AdofaiEvent } from '../structure/interfaces';

export interface Multitap extends AdofaiEvent {
    eventType: 'Multitap';
    angleOffset?: number;
}
