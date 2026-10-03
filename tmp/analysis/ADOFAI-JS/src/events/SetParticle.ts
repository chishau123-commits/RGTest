import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface SetParticle extends AdofaiEvent {
    eventType: 'SetParticle';
    tag?: string;
    duration?: number;
    positionOffset?: Vec2;
    rotationOffset?: number;
    scale?: number;
    opacity?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    easing?: string;
    angleOffset?: number;
}
