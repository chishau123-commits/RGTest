import type { AdofaiEvent } from '../structure/interfaces';
import type { Condition } from '../types';

export interface SetConditionalEvents extends AdofaiEvent {
    eventType: 'SetConditionalEvents';
    eventTag?: string;
    condition?: Condition;
    execution?: string;
    events?: any[];
}
