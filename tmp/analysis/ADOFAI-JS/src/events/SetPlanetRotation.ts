import type { AdofaiEvent } from '../structure/interfaces';

export interface SetPlanetRotation extends AdofaiEvent {
    eventType: 'SetPlanetRotation';
    ease?: string;
    easeParts?: number;
    easing?: string;
    duration?: number;
    angleOffset?: number;
}
