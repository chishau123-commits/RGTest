import type { AdofaiEvent } from '../structure/interfaces';

export interface ShakeScreen extends AdofaiEvent {
    eventType: 'ShakeScreen';
    duration?: number;
    intensity?: number;
    intensityTo?: number;
    intensityToDuration?: number;
    intensityToEasing?: string;
    easing?: string;
    angleOffset?: number;
}
