import type { AdofaiEvent } from '../structure/interfaces';

export interface ScreenScroll extends AdofaiEvent {
    eventType: 'ScreenScroll';
    duration?: number;
    scrollX?: number;
    scrollY?: number;
    easing?: string;
    angleOffset?: number;
}
