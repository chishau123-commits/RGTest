import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, TextAnchor, Hitbox, ABoolean } from '../types';

export interface AddDecoration extends AdofaiEvent {
    eventType: 'AddDecoration';
    decText?: string;
    decTextColor?: string;
    decTextFont?: string;
    decTextAnchor?: TextAnchor;
    decTextScale?: number;
    decTextAngle?: number;
    decTextDepth?: number;
    decorationImage?: string;
    angleOffset?: number;
    position?: Vec2;
    rotation?: number;
    scale?: Vec2;
    parallax?: Vec2;
    opacity?: number;
    color?: string;
    depth?: number;
    tag?: string;
    imageSmoothing?: ABoolean;
    hitbox?: Hitbox;
    failsafe?: ABoolean;
}
