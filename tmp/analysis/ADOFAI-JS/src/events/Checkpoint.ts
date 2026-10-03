import type { AdofaiEvent } from '../structure/interfaces';

export interface Checkpoint extends AdofaiEvent {
    eventType: 'Checkpoint';
}
