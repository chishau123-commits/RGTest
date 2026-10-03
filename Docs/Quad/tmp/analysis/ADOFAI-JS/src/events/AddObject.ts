import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, ABoolean } from '../types';

export interface AddObject extends AdofaiEvent {
    eventType: 'AddObject';
    object?: string;
    position?: Vec2;
    rotation?: number;
    scale?: Vec2;
    opacity?: number;
    parallax?: Vec2;
    tag?: string;
    depth?: number;
    color?: string;
    imageSmoothing?: ABoolean;
    angleOffset?: number;
}
