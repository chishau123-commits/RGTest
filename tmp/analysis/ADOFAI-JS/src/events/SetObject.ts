import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface SetObject extends AdofaiEvent {
    eventType: 'SetObject';
    tag?: string;
    duration?: number;
    positionOffset?: Vec2;
    rotationOffset?: number;
    scale?: Vec2;
    opacity?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    easing?: string;
    angleOffset?: number;
}
