import type { AdofaiEvent } from '../structure/interfaces';

export interface AddComponent extends AdofaiEvent {
    eventType: 'AddComponent';
    component?: string;
    components?: string;
    angleOffset?: number;
}
