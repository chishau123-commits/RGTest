import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface MoveDecorations extends AdofaiEvent {
    eventType: 'MoveDecorations';
    duration?: number;
    tag?: string;
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
    parallaxOffset?: Vec2;
    pivotOffset?: Vec2;
}
