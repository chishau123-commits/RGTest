import type { AdofaiEvent } from '../structure/interfaces';

export interface RepeatEvents extends AdofaiEvent {
    eventType: 'RepeatEvents';
    repetitions?: number;
    interval?: number;
    intervalRandom?: number;
    easing?: string;
    duration?: number;
    angleOffset?: number;
}
