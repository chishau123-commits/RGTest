import type { AdofaiEvent } from '../structure/interfaces';
import type { AngleCorrectionDir } from '../types';

export interface Pause extends AdofaiEvent {
    eventType: 'Pause';
    duration?: number;
    angleCorrectionDir?: AngleCorrectionDir;
    countdownTicks?: number;
    angleOffset?: number;
}
