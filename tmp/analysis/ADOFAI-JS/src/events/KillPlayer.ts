import type { AdofaiEvent } from '../structure/interfaces';

export interface KillPlayer extends AdofaiEvent {
    eventType: 'KillPlayer';
    angleOffset?: number;
}
