import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2 } from '../types';

export interface SetText extends AdofaiEvent {
    eventType: 'SetText';
    decText?: string;
    tag?: string;
    duration?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    fadeIn?: number;
    fadeOut?: number;
    easing?: string;
    positionOffset?: Vec2;
    rotationOffset?: number;
    scale?: Vec2;
    opacity?: number;
}
