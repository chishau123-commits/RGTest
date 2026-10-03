import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface ScaleRadius extends AdofaiEvent {
    eventType: 'ScaleRadius';
    scale?: number;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
