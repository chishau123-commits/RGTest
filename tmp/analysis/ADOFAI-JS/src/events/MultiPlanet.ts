import type { AdofaiEvent } from '../structure/interfaces';

export interface MultiPlanet extends AdofaiEvent {
    eventType: 'MultiPlanet';
    planets?: number;
    angleOffset?: number;
}
