import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface ScaleMargin extends AdofaiEvent {
    eventType: 'ScaleMargin';
    scale?: number;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
