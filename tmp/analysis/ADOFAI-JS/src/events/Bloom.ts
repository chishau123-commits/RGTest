import type { AdofaiEvent } from '../structure/interfaces';

export interface Bloom extends AdofaiEvent {
    eventType: 'Bloom';
    duration?: number;
    intensity?: number;
    intensityTo?: number;
    intensityToDuration?: number;
    intensityToEasing?: string;
    threshold?: number;
    thresholdTo?: number;
    thresholdToDuration?: number;
    thresholdToEasing?: string;
    easing?: string;
    angleOffset?: number;
}
