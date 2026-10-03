import type { AdofaiEvent } from '../structure/interfaces';

export interface HallOfMirrors extends AdofaiEvent {
    eventType: 'HallOfMirrors';
    duration?: number;
    angleOffset?: number;
}
