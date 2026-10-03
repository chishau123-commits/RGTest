import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface SetFloorIcon extends AdofaiEvent {
    eventType: 'SetFloorIcon';
    floorIcon?: string;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
