import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface FreeRoamRemove extends AdofaiEvent {
    eventType: 'FreeRoamRemove';
    editorOnly?: ABoolean;
    angleOffset?: number;
}
