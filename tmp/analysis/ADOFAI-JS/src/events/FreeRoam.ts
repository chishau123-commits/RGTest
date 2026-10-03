import type { AdofaiEvent } from '../structure/interfaces';
import type { AngleCorrectionDir, ABoolean } from '../types';

export interface FreeRoam extends AdofaiEvent {
    eventType: 'FreeRoam';
    duration?: number;
    angleCorrectionDir?: AngleCorrectionDir;
    freeRoamAngle?: number;
    freeRoamAngleLocal?: ABoolean;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
