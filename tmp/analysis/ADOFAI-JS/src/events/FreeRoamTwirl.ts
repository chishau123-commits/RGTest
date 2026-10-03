import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface FreeRoamTwirl extends AdofaiEvent {
    eventType: 'FreeRoamTwirl';
    editorOnly?: ABoolean;
    angleOffset?: number;
}
