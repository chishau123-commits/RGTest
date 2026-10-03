import type { AdofaiEvent } from '../structure/interfaces';
import type { FlashStyle } from '../types';

export interface Flash extends AdofaiEvent {
    eventType: 'Flash';
    duration?: number;
    color?: string;
    colorTo?: string;
    colorToDuration?: number;
    colorToEasing?: string;
    flashStyle?: FlashStyle;
    opacity?: number;
    easing?: string;
    angleOffset?: number;
}
