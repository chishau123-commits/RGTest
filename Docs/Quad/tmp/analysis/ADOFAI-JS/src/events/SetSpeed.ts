import type { AdofaiEvent } from '../structure/interfaces';

export interface SetSpeed extends AdofaiEvent {
    eventType: 'SetSpeed';
    speed?: number;
}
