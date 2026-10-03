import type { AdofaiEvent } from '../structure/interfaces';

export interface CallMethod extends AdofaiEvent {
    eventType: 'CallMethod';
    method?: string;
    target?: string;
    parameters?: string;
    angleOffset?: number;
}
