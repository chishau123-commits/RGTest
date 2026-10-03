import type { AdofaiEvent } from '../structure/interfaces';
import type { TargetPlanet } from '../types';

export interface ScalePlanets extends AdofaiEvent {
    eventType: 'ScalePlanets';
    planetsScale?: number;
    scale?: number;
    duration?: number;
    easing?: string;
    targetPlanet?: TargetPlanet;
    planetNumber?: number;
    angleOffset?: number;
}
