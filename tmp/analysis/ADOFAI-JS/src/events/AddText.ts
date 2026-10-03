import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, TextAnchor } from '../types';

export interface AddText extends AdofaiEvent {
    eventType: 'AddText';
    decText?: string;
    decTextColor?: string;
    decTextFont?: string;
    decTextAnchor?: TextAnchor;
    decTextScale?: number;
    decTextAngle?: number;
    decTextDepth?: number;
    decTextFontSize?: number;
    angleOffset?: number;
    position?: Vec2;
    rotation?: number;
    scale?: Vec2;
    parallax?: Vec2;
    opacity?: number;
    color?: string;
    depth?: number;
    tag?: string;
}
