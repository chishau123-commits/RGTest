import type { AdofaiEvent } from '../structure/interfaces';
import type { Vec2, RelativeTo, ABoolean } from '../types';

export interface MoveCamera extends AdofaiEvent {
    eventType: 'MoveCamera';
    duration?: number;
    relativeTo?: RelativeTo;
    position?: Vec2;
    rotation?: number;
    zoom?: number;
    minZoom?: number;
    maxZoom?: number;
    easing?: string;
    editorOnly?: ABoolean;
}
